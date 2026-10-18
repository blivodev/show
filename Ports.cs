using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace show;

// show ports [port or process/service name]
public class Ports
{
	private record Listener(string Protocol, string Address, int Port, int Pid);

	// neighbouring ports from one process, e.g. 5001-5300
	private record PortRange(string Protocol, string Address, int Pid, int First, int Last);

	// so 445 reads as SMB
	static readonly Dictionary<int, string> KnownPorts = new()
	{
		{ 21, "FTP" }, { 22, "SSH" }, { 23, "Telnet" }, { 25, "SMTP" }, { 53, "DNS" },
		{ 67, "DHCP" }, { 68, "DHCP" }, { 80, "HTTP" }, { 88, "Kerberos" }, { 123, "NTP" },
		{ 135, "RPC" }, { 137, "NetBIOS" }, { 138, "NetBIOS" }, { 139, "NetBIOS" },
		{ 389, "LDAP" }, { 443, "HTTPS" }, { 445, "SMB" }, { 1433, "SQL Server" },
		{ 1900, "UPnP" }, { 3306, "MySQL" }, { 3389, "RDP" }, { 5353, "mDNS" },
		{ 5432, "PostgreSQL" }, { 5900, "VNC" }, { 8080, "HTTP alt" }
	};

	public void Display(string filter = "")
	{
		var listeners = GetListeners().Distinct().ToList();
		var services = GetServices();

		// one lookup per process
		var processNames = new Dictionary<int, string>();
		foreach (int pid in listeners.Select(l => l.Pid).Distinct())
		{
			processNames[pid] = ProcessName(pid);
		}

		if (filter != "")
		{
			if (int.TryParse(filter, out int port))
			{
				listeners = listeners.Where(l => l.Port == port).ToList();

				if (listeners.Count == 0)
				{
					AnsiConsole.MarkupLine($"[red]Port {port} is closed[/] - nothing is listening on it.");
					return;
				}
			}
			else
			{
				listeners = listeners.Where(l =>
					processNames[l.Pid].Contains(filter, StringComparison.OrdinalIgnoreCase) ||
					ServiceName(services, l.Pid).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

				if (listeners.Count == 0)
				{
					AnsiConsole.MarkupLine($"[red]No open ports found[/] for '{Markup.Escape(filter)}'.");
					return;
				}
			}
		}

		var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
		// short columns never wrap, a narrow window squeezes the others
		table.AddColumn(new TableColumn("[yellow]Port[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]Proto[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]Status[/]").NoWrap());
		table.AddColumn("[yellow]Known as[/]");
		table.AddColumn("[yellow]Process[/]");
		table.AddColumn(new TableColumn("[yellow]PID[/]").NoWrap());
		table.AddColumn("[yellow]Service[/]");
		table.AddColumn(new TableColumn("[yellow]Address[/]").NoWrap());

		int anyNetwork = 0;

		foreach (var r in Group(listeners))
		{
			// 0.0.0.0 and :: = all adapters, loopback = this machine only
			bool everywhere = r.Address == "0.0.0.0" || r.Address == "::";
			bool localOnly = r.Address == "127.0.0.1" || r.Address == "::1";
			string addressColour = everywhere ? "yellow" : localOnly ? "green" : "cyan";

			string process = $"[yellow]{Markup.Escape(processNames[r.Pid])}[/]";
			string service = Markup.Escape(ServiceName(services, r.Pid));
			string address = $"[{addressColour}]{Markup.Escape(r.Address)}[/]";

			// 5+ ports in a row become one row
			if (r.Last - r.First >= 4)
			{
				int count = r.Last - r.First + 1;
				table.AddRow($"[cyan]{r.First}-{r.Last}[/]", $"[grey]{r.Protocol}[/]", "[green]Open[/]", $"[grey]{count} ports[/]", process, $"[grey]{r.Pid}[/]", service, address);
				if (everywhere) anyNetwork += count;
			}
			else
			{
				for (int port = r.First; port <= r.Last; port++)
				{
					string known = KnownPorts.ContainsKey(port) ? KnownPorts[port] : "-";
					table.AddRow($"[cyan]{port}[/]", $"[grey]{r.Protocol}[/]", "[green]Open[/]", known, process, $"[grey]{r.Pid}[/]", service, address);
					if (everywhere) anyNetwork++;
				}
			}
		}

		AnsiConsole.Write(table);

		int tcp = listeners.Count(l => l.Protocol == "TCP");
		int udp = listeners.Count(l => l.Protocol == "UDP");
		AnsiConsole.MarkupLine($"[grey]{listeners.Count} open ports ({tcp} TCP, {udp} UDP).[/]");
		AnsiConsole.MarkupLine($"[yellow]Yellow[/] [grey]address = reachable from any network ({anyNetwork} ports),[/] [green]green[/] [grey]= this PC only.[/]");
	}


	// netstat on windows, ss on linux, lsof on mac
	private static List<Listener> GetListeners()
	{
		List<Listener> listeners;

		if (OperatingSystem.IsWindows())
		{
			listeners = ParseNetstat(Tools.RunProgram("netstat", "-ano"));
		}
		else if (OperatingSystem.IsLinux())
		{
			listeners = ParseSs(Tools.RunProgram("ss", "-H -lntup"));
		}
		else if (OperatingSystem.IsMacOS())
		{
			listeners = ParseLsof(Tools.RunProgram("lsof", "-nP -iTCP -sTCP:LISTEN")).Concat(ParseLsof(Tools.RunProgram("lsof", "-nP -iUDP"))).ToList();
		}
		else
		{
			listeners = new List<Listener>();
		}

		// tool missing: .NET still knows the ports, just not the process
		return listeners.Count > 0 ? listeners : FromDotNet();
	}

	// "[::1]:80", "*:5353", "127.0.0.53%lo:53" -> address, port (-1 if no port)
	private static (string Address, int Port) SplitEndpoint(string text)
	{
		int colon = text.LastIndexOf(':');
		if (colon < 0 || !int.TryParse(text.Substring(colon + 1), out int port))
		{
			return ("", -1);
		}

		string address = text.Substring(0, colon).Trim('[', ']').Split('%')[0];
		return (address == "*" ? "0.0.0.0" : address, port);
	}

	// netstat -ano
	//   TCP  0.0.0.0:135  0.0.0.0:0  LISTENING  2020
	//   UDP  0.0.0.0:53   *:*                    4188
	private static List<Listener> ParseNetstat(string output)
	{
		var listeners = new List<Listener>();

		foreach (var line in output.Split('\n'))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			bool tcp = p.Length == 5 && p[0] == "TCP";
			bool udp = p.Length == 4 && p[0] == "UDP";
			if (!tcp && !udp) continue;

			// a listening tcp port has no remote end (the word LISTENING depends on the OS language)
			if (tcp && p[2] != "0.0.0.0:0" && p[2] != "[::]:0") continue;

			var (address, port) = SplitEndpoint(p[1]);
			if (port < 0 || !int.TryParse(p[^1], out int pid)) continue;

			listeners.Add(new Listener(p[0], address, port, pid));
		}

		return listeners;
	}

	// ss -H -lntup
	//   tcp  LISTEN  0  4096  0.0.0.0:22  0.0.0.0:*  users:(("sshd",pid=812,fd=3))
	// without root it only knows the process for your own ports
	private static List<Listener> ParseSs(string output)
	{
		var listeners = new List<Listener>();

		foreach (var line in output.Split('\n'))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (p.Length < 6) continue;

			string protocol = p[0] == "tcp" ? "TCP" : p[0] == "udp" ? "UDP" : "";
			var (address, port) = SplitEndpoint(p[4]);
			if (protocol == "" || port < 0) continue;

			var pidText = p.Length > 6 ? Regex.Match(p[6], @"pid=(\d+)") : Match.Empty;
			int pid = pidText.Success ? int.Parse(pidText.Groups[1].Value) : 0;

			listeners.Add(new Listener(protocol, address, port, pid));
		}

		return listeners;
	}

	// lsof -nP -iTCP -sTCP:LISTEN (and -iUDP)
	//   COMMAND  PID  USER  FD  TYPE  DEVICE  SIZE/OFF  NODE  NAME
	//   ControlC 512  me    8u  IPv4  0x1234  0t0       TCP   *:7000 (LISTEN)
	private static List<Listener> ParseLsof(string output)
	{
		var listeners = new List<Listener>();

		foreach (var line in output.Split('\n'))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (p.Length < 9 || p[0] == "COMMAND" || (p[7] != "TCP" && p[7] != "UDP")) continue;

			var (address, port) = SplitEndpoint(p[8]);
			if (port < 0 || !int.TryParse(p[1], out int pid)) continue;

			listeners.Add(new Listener(p[7], address, port, pid));
		}

		return listeners;
	}

	private static List<Listener> FromDotNet()
	{
		var listeners = new List<Listener>();
		var properties = IPGlobalProperties.GetIPGlobalProperties();

		foreach (var endpoint in properties.GetActiveTcpListeners())
		{
			listeners.Add(new Listener("TCP", endpoint.Address.ToString().Split('%')[0], endpoint.Port, 0));
		}

		foreach (var endpoint in properties.GetActiveUdpListeners())
		{
			listeners.Add(new Listener("UDP", endpoint.Address.ToString().Split('%')[0], endpoint.Port, 0));
		}

		return listeners;
	}

	private static List<PortRange> Group(List<Listener> listeners)
	{
		var ranges = new List<PortRange>();

		foreach (var l in listeners.OrderBy(l => l.Protocol).ThenBy(l => l.Address).ThenBy(l => l.Pid).ThenBy(l => l.Port))
		{
			var last = ranges.Count > 0 ? ranges[^1] : null;

			if (last != null && last.Protocol == l.Protocol && last.Address == l.Address && last.Pid == l.Pid && last.Last + 1 == l.Port)
			{
				ranges[^1] = last with { Last = l.Port };
			}
			else
			{
				ranges.Add(new PortRange(l.Protocol, l.Address, l.Pid, l.Port, l.Port));
			}
		}

		return ranges.OrderBy(r => r.First).ThenBy(r => r.Protocol).ToList();
	}

	private static string ProcessName(int pid)
	{
		if (pid == 0) return OperatingSystem.IsWindows() ? "System Idle" : "-";

		try
		{
			return Process.GetProcessById(pid).ProcessName;
		}
		catch
		{
			return "-"; // the process closed, or we aren't allowed to look
		}
	}

	private static Dictionary<int, List<string>> GetServices()
	{
		var services = new Dictionary<int, List<string>>();

		try
		{
			foreach (var service in Pc.ReadServices())
			{
				if (!services.ContainsKey(service.Pid))
				{
					services[service.Pid] = new List<string>();
				}
				services[service.Pid].Add(service.Display);
			}
		}
		catch (Exception)
		{
		}

		return services;
	}

	private static string ServiceName(Dictionary<int, List<string>> services, int pid)
	{
		if (!services.ContainsKey(pid)) return "-";

		var names = services[pid];
		string name = names[0].Length > 22 ? names[0].Substring(0, 21) + "…" : names[0];
		return names.Count == 1 ? name : name + " (+" + (names.Count - 1) + ")";
	}
}
