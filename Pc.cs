using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace show;

// show pc [all|os|cpu|gpu|ram|board|disk]
public class Pc
{
	static readonly string[] Sections = { "os", "cpu", "gpu", "ram", "board", "disk" };

	public void Display(string section)
	{
		section = section.ToLower();

		if (section != "all" && !Sections.Contains(section))
		{
			Console.WriteLine($"Unknown pc section: {section}");
			Console.WriteLine("Use: all, " + string.Join(", ", Sections));
			return;
		}

		if (section == "all" || section == "os") ShowOs();
		if (section == "all" || section == "cpu") ShowCpu();
		if (section == "all" || section == "gpu") ShowGpu();
		if (section == "all" || section == "ram") ShowRam();
		if (section == "all" || section == "board") ShowBoard();
		if (section == "all" || section == "disk") ShowDisks();
	}

	private void ShowOs()
	{
		if (!OperatingSystem.IsWindows())
		{
			ShowOsUnix();
			return;
		}

		var table = NewTable("Operating system", "Computer", "OS", "Version", "Build", "Architecture");

		foreach (var os in Query("Win32_OperatingSystem"))
		{
			table.AddRow(Markup.Escape(Environment.MachineName), Get(os, "Caption"), Get(os, "Version"), Get(os, "BuildNumber"), Get(os, "OSArchitecture"));
		}

		AnsiConsole.Write(table);
	}

	private void ShowCpu()
	{
		if (!OperatingSystem.IsWindows())
		{
			ShowCpuUnix();
			return;
		}

		var table = NewTable("CPU", "Name", "Cores", "Threads", "Max speed (MHz)");

		foreach (var cpu in Query("Win32_Processor"))
		{
			table.AddRow(Get(cpu, "Name"), Get(cpu, "NumberOfCores"), Get(cpu, "NumberOfLogicalProcessors"), Get(cpu, "MaxClockSpeed"));
		}

		AnsiConsole.Write(table);
	}

	private void ShowGpu()
	{
		if (!OperatingSystem.IsWindows())
		{
			ShowGpuUnix();
			return;
		}

		var table = NewTable("GPU", "Name", "Driver version", "Resolution");

		foreach (var gpu in Query("Win32_VideoController"))
		{
			string resolution = Get(gpu, "CurrentHorizontalResolution") + " x " + Get(gpu, "CurrentVerticalResolution");
			table.AddRow(Get(gpu, "Name"), Get(gpu, "DriverVersion"), resolution);
		}

		AnsiConsole.Write(table);
	}

	private void ShowRam()
	{
		if (!OperatingSystem.IsWindows())
		{
			ShowRamUnix();
			return;
		}

		// the total comes from the computer system, the sticks are listed below
		long total = 0;
		foreach (var cs in Query("Win32_ComputerSystem"))
		{
			total = Convert.ToInt64(cs["TotalPhysicalMemory"]);
		}

		var table = NewTable("RAM (total " + Formatter.FormatSize(total) + ")", "Slot", "Manufacturer", "Size", "Speed (MHz)");

		foreach (var ram in Query("Win32_PhysicalMemory"))
		{
			table.AddRow(Get(ram, "DeviceLocator"), Get(ram, "Manufacturer"), Size(ram, "Capacity"), Get(ram, "Speed"));
		}

		AnsiConsole.Write(table);
	}

	private void ShowBoard()
	{
		if (!OperatingSystem.IsWindows())
		{
			ShowBoardUnix();
			return;
		}

		var table = NewTable("Motherboard", "Manufacturer", "Model", "BIOS version");

		string bios = "-";
		foreach (var b in Query("Win32_BIOS"))
		{
			bios = Get(b, "SMBIOSBIOSVersion");
		}

		foreach (var board in Query("Win32_BaseBoard"))
		{
			table.AddRow(Get(board, "Manufacturer"), Get(board, "Product"), bios);
		}

		AnsiConsole.Write(table);
	}

	private void ShowDisks()
	{
		if (!OperatingSystem.IsWindows())
		{
			ShowDisksUnix();
			return;
		}

		var table = NewTable("Disks", "Model", "Size", "Interface", "Type");

		foreach (var disk in Query("Win32_DiskDrive"))
		{
			table.AddRow(Get(disk, "Model"), Size(disk, "Size"), Get(disk, "InterfaceType"), Get(disk, "MediaType"));
		}

		AnsiConsole.Write(table);
	}

	// linux and mac: files in /proc and /sys, or a helper program. Anything missing shows as -

	private static string ReadText(string path)
	{
		try
		{
			return System.IO.File.ReadAllText(path).Trim();
		}
		catch (Exception)
		{
			return "";
		}
	}

	// value of the first "key : value" line
	private static string Field(string text, string key)
	{
		foreach (var line in text.Split('\n'))
		{
			int colon = line.IndexOf(':');
			if (colon > 0 && line.Substring(0, colon).Trim() == key)
			{
				return line.Substring(colon + 1).Trim();
			}
		}

		return "";
	}

	private static string Dash(string text)
	{
		return text == "" ? "-" : Markup.Escape(text);
	}

	private void ShowOsUnix()
	{
		string os = RuntimeInformation.OSDescription;
		string kernel = "";

		if (OperatingSystem.IsLinux())
		{
			// PRETTY_NAME="Ubuntu 24.04 LTS" in /etc/os-release
			string name = Regex.Match(ReadText("/etc/os-release"), @"^PRETTY_NAME=""?([^""\n]*)""?", RegexOptions.Multiline).Groups[1].Value;
			if (name != "") os = name;
			kernel = ReadText("/proc/sys/kernel/osrelease");
		}
		else if (OperatingSystem.IsMacOS())
		{
			os = "macOS " + Tools.RunProgram("sw_vers", "-productVersion").Trim();
			kernel = Tools.RunProgram("uname", "-r").Trim();
		}

		var table = NewTable("Operating system", "Computer", "OS", "Kernel", "Architecture");
		table.AddRow(Markup.Escape(Environment.MachineName), Dash(os), Dash(kernel), RuntimeInformation.OSArchitecture.ToString());
		AnsiConsole.Write(table);
	}

	private void ShowCpuUnix()
	{
		string name = "";
		string cores = "";
		string maxSpeed = "";

		if (OperatingSystem.IsLinux())
		{
			string cpuinfo = ReadText("/proc/cpuinfo");

			name = Field(cpuinfo, "model name");
			if (name == "") name = Field(cpuinfo, "Model"); // Arm computers
			if (name == "") name = Field(Tools.RunProgram("lscpu"), "Model name");

			// cpuinfo lists every thread, so count the unique (physical id, core id) pairs
			var coreIds = new HashSet<string>();
			string physical = "";
			foreach (var line in cpuinfo.Split('\n'))
			{
				if (line.StartsWith("physical id")) physical = line.Split(':')[1].Trim();
				if (line.StartsWith("core id")) coreIds.Add(physical + ":" + line.Split(':')[1].Trim());
			}
			if (coreIds.Count > 0) cores = coreIds.Count.ToString();

			// kHz, if the system exposes it
			if (long.TryParse(ReadText("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq"), out long kHz))
			{
				maxSpeed = (kHz / 1000).ToString();
			}
		}
		else if (OperatingSystem.IsMacOS())
		{
			name = Tools.RunProgram("sysctl", "-n machdep.cpu.brand_string").Trim();
			cores = Tools.RunProgram("sysctl", "-n hw.physicalcpu").Trim();
		}

		var table = NewTable("CPU", "Name", "Cores", "Threads", "Max speed (MHz)");
		table.AddRow(Dash(name), Dash(cores), Environment.ProcessorCount.ToString(), Dash(maxSpeed));
		AnsiConsole.Write(table);
	}

	private void ShowGpuUnix()
	{
		var names = new List<string>();

		if (OperatingSystem.IsLinux())
		{
			// e.g. 00:02.0 VGA compatible controller: Intel Corporation ...
			foreach (var line in Tools.RunProgram("lspci").Split('\n'))
			{
				int colon = line.IndexOf(": ");
				if (colon > 0 && (line.Contains("VGA compatible controller") || line.Contains("3D controller") || line.Contains("Display controller")))
				{
					names.Add(line.Substring(colon + 2).Trim());
				}
			}
		}
		else if (OperatingSystem.IsMacOS())
		{
			foreach (var line in Tools.RunProgram("system_profiler", "SPDisplaysDataType").Split('\n'))
			{
				if (line.Contains("Chipset Model:")) names.Add(line.Split(':')[1].Trim());
			}
		}

		if (names.Count == 0)
		{
			AnsiConsole.MarkupLine("[grey]No GPU information available on this system" + (OperatingSystem.IsLinux() ? " (it needs the lspci program)." : ".") + "[/]");
			return;
		}

		var table = NewTable("GPU", "Name");
		foreach (var name in names)
		{
			table.AddRow(Markup.Escape(name));
		}
		AnsiConsole.Write(table);
	}

	private void ShowRamUnix()
	{
		long total = TotalRam;
		string used = "";
		string available = "";

		if (OperatingSystem.IsLinux())
		{
			string meminfo = ReadText("/proc/meminfo");
			long totalBytes = KiloBytes(meminfo, "MemTotal") * 1024;
			long availableBytes = KiloBytes(meminfo, "MemAvailable") * 1024;

			if (totalBytes > 0) total = totalBytes;
			if (availableBytes > 0)
			{
				available = Formatter.FormatSize(availableBytes);
				used = Formatter.FormatSize(total - availableBytes);
			}
		}
		else if (OperatingSystem.IsMacOS())
		{
			if (long.TryParse(Tools.RunProgram("sysctl", "-n hw.memsize").Trim(), out long bytes)) total = bytes;
		}

		var table = NewTable("RAM", "Total", "Used", "Available");
		table.AddRow(Formatter.FormatSize(total), Dash(used), Dash(available));
		AnsiConsole.Write(table);
	}

	private static long KiloBytes(string meminfo, string key)
	{
		return long.TryParse(Field(meminfo, key).Replace("kB", "").Trim(), out long kb) ? kb : 0;
	}

	private void ShowBoardUnix()
	{
		string maker = "";
		string model = "";
		string bios = "";

		if (OperatingSystem.IsLinux())
		{
			maker = ReadText("/sys/class/dmi/id/board_vendor");
			model = ReadText("/sys/class/dmi/id/board_name");
			bios = ReadText("/sys/class/dmi/id/bios_version");
		}
		else if (OperatingSystem.IsMacOS())
		{
			maker = "Apple";
			model = Tools.RunProgram("sysctl", "-n hw.model").Trim();
		}

		if (maker == "" && model == "" && bios == "")
		{
			AnsiConsole.MarkupLine("[grey]No motherboard information available on this system.[/]");
			return;
		}

		var table = NewTable("Motherboard", "Manufacturer", "Model", "BIOS version");
		table.AddRow(Dash(maker), Dash(model), Dash(bios));
		AnsiConsole.Write(table);
	}

	private void ShowDisksUnix()
	{
		if (!OperatingSystem.IsLinux())
		{
			AnsiConsole.MarkupLine("[grey]Disk details are only available on Windows and Linux. Use show drives to see the mounted drives.[/]");
			return;
		}

		var table = NewTable("Disks", "Model", "Size", "Interface", "Type");

		// lsblk -P: one disk per line as KEY="value" pairs
		foreach (var line in Tools.RunProgram("lsblk", "-d -n -b -P -o NAME,MODEL,SIZE,TRAN,ROTA,TYPE").Split('\n'))
		{
			var values = new Dictionary<string, string>();
			foreach (Match m in Regex.Matches(line, @"(\w+)=""([^""]*)"""))
			{
				values[m.Groups[1].Value] = m.Groups[2].Value;
			}

			if (values.GetValueOrDefault("TYPE") != "disk") continue;

			string model = values.GetValueOrDefault("MODEL", "").Trim();
			long.TryParse(values.GetValueOrDefault("SIZE", ""), out long size);

			table.AddRow(
				Dash(model == "" ? values.GetValueOrDefault("NAME", "") : model),
				Formatter.FormatSize(size),
				Dash(values.GetValueOrDefault("TRAN", "")),
				values.GetValueOrDefault("ROTA") == "0" ? "SSD" : "HDD");
		}

		if (table.Rows.Count == 0)
		{
			AnsiConsole.MarkupLine("[grey]No disks found (this needs the lsblk program).[/]");
			return;
		}

		AnsiConsole.Write(table);
	}

	// every OS keeps its own list of services
	internal static List<(string Name, string Display, string Start, int Pid)> ReadServices()
	{
		if (OperatingSystem.IsWindows()) return ReadServicesWindows();
		if (OperatingSystem.IsLinux()) return ReadServicesLinux();
		if (OperatingSystem.IsMacOS()) return ReadServicesMac();

		throw new PlatformNotSupportedException("Services can't be listed on this system.");
	}

	private static List<(string Name, string Display, string Start, int Pid)> ReadServicesLinux()
	{
		var services = new List<(string Name, string Display, string Start, int Pid)>();

		var units = Tools.RunProgram("systemctl", "list-units --type=service --state=running --no-legend --plain --no-pager")
			.Split('\n')
			.Select(line => line.Trim().Split(' ')[0])
			.Where(unit => unit.EndsWith(".service"))
			.ToList();

		if (units.Count == 0)
		{
			return services; // no systemd here (a container, for example)
		}

		// one systemctl call for all of them, blocks separated by a blank line
		string details = Tools.RunProgram("systemctl", "show --no-pager -p Id -p Description -p MainPID -p UnitFileState " + string.Join(" ", units));

		foreach (var block in details.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
		{
			var values = new Dictionary<string, string>();
			foreach (var line in block.Split('\n'))
			{
				var pair = line.Split('=', 2);
				if (pair.Length == 2) values[pair[0]] = pair[1];
			}

			string name = values.GetValueOrDefault("Id", "").Replace(".service", "");
			if (name == "") continue;

			// enabled = starts on boot, static = only when needed
			string state = values.GetValueOrDefault("UnitFileState", "");
			string start = state.StartsWith("enabled") ? "Auto" : state == "disabled" ? "Disabled" : state == "" ? "-" : char.ToUpper(state[0]) + state.Substring(1);

			int.TryParse(values.GetValueOrDefault("MainPID", "0"), out int pid);
			services.Add((name, values.GetValueOrDefault("Description", name), start, pid));
		}

		return services;
	}

	private static List<(string Name, string Display, string Start, int Pid)> ReadServicesMac()
	{
		var services = new List<(string Name, string Display, string Start, int Pid)>();

		// launchctl list: PID Status Label, PID is - when not running
		foreach (var line in Tools.RunProgram("launchctl", "list").Split('\n'))
		{
			var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

			if (p.Length >= 3 && int.TryParse(p[0], out int pid))
			{
				services.Add((p[2], p[2], "-", pid));
			}
		}

		return services;
	}

	// windows reads it directly, everything else goes through Process
	private static Dictionary<int, Counter> ReadCounters()
	{
		return OperatingSystem.IsWindows() ? ReadCountersWindows() : ReadCountersPortable();
	}

	// macOS only lets you read cpu time for your own processes
	private static Dictionary<int, Counter> ReadCountersPortable()
	{
		var counters = new Dictionary<int, Counter>();
		ulong at = (ulong)(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 10_000_000);

		foreach (var p in Process.GetProcesses())
		{
			try
			{
				counters[p.Id] = new Counter((ulong)p.TotalProcessorTime.Ticks, at, (ulong)p.WorkingSet64);
			}
			catch (Exception)
			{
				// no access, or already gone
			}
			finally
			{
				p.Dispose();
			}
		}

		return counters;
	}

	private static HashSet<int> CurrentUserProcessIds()
	{
		var ids = new HashSet<int>();
		string uid = Tools.RunProgram("id", "-u").Trim();

		foreach (var line in Tools.RunProgram("ps", "-axo pid=,uid=").Split('\n'))
		{
			var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			if (p.Length == 2 && p[1] == uid && int.TryParse(p[0], out int pid))
			{
				ids.Add(pid);
			}
		}

		return ids;
	}


	// table with a yellow title and cyan headers
	private static Table NewTable(string title, params string[] columns)
	{
		var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
		table.Title("[yellow]" + title + "[/]");

		foreach (var c in columns)
		{
			table.AddColumn("[cyan]" + c + "[/]");
		}

		return table;
	}

	// WMI query
	[SupportedOSPlatform("windows")]
	private static List<ManagementObject> Query(string wmiClass, string where = "")
	{
		var results = new List<ManagementObject>();

		using var searcher = new ManagementObjectSearcher("SELECT * FROM " + wmiClass + (where == "" ? "" : " WHERE " + where));
		foreach (ManagementObject item in searcher.Get())
		{
			results.Add(item);
		}

		return results;
	}

	// property as text, - if empty
	[SupportedOSPlatform("windows")]
	private static string Get(ManagementObject item, string property)
	{
		var value = item[property];
		return value == null ? "-" : Markup.Escape(value.ToString()!.Trim());
	}

	// bytes property as KB/MB/GB
	[SupportedOSPlatform("windows")]
	private static string Size(ManagementObject item, string property)
	{
		var value = item[property];
		return value == null ? "-" : Formatter.FormatSize(Convert.ToInt64(value));
	}


	// Ram is the private working set (what Task Manager calls Memory)
	// Cores = CPU used in the last second, 0.5 = half a core
	private record ProcInfo(int Pid, string Name, long Ram, bool CpuKnown, TimeSpan CpuTime, double Cores, int Threads, string Title, DateTime? Started);

	static readonly long TotalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

	// show services [filter]
	public void DisplayServices(string filter = "", bool useRegex = false)
	{
		Regex? matcher;
		try
		{
			matcher = filter == "" ? null : Tools.MakeMatcher(filter, useRegex, true);
		}
		catch (ArgumentException ex)
		{
			AnsiConsole.MarkupLine("[red]That is not a valid regular expression:[/] " + Markup.Escape(ex.Message));
			return;
		}

		var processes = Sample();

		var everyService = new List<(string Display, string Name, string Start, ProcInfo? Proc, int Pid)>();
		try
		{
			foreach (var sv in ReadServices())
			{
				processes.TryGetValue(sv.Pid, out var proc);
				everyService.Add((sv.Display, sv.Name, sv.Start, proc, sv.Pid));
			}
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine("[red]Could not read the list of services from Windows:[/] " + Markup.Escape(ex.Message));
			return;
		}

		// filter matches the service name, short name or the process (svchost, ...)
		var services = everyService
			.Where(x => matcher == null || Tools.IsMatch(matcher, x.Display) || Tools.IsMatch(matcher, x.Name) || (x.Proc != null && Tools.IsMatch(matcher, x.Proc.Name)))
			.ToList();

		if (services.Count == 0)
		{
			AnsiConsole.MarkupLine(filter == "" ? "[yellow]No running services found.[/]" : "[yellow]No running services match[/] '" + Markup.Escape(filter) + "'.");
			return;
		}

		var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
		table.AddColumn("[yellow]Service[/]");
		table.AddColumn("[yellow]Name[/]");
		table.AddColumn(new TableColumn("[yellow]PID[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]RAM[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]RAM %[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]CPU %[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]CPU time[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]Cores[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]Start[/]").NoWrap());

		bool anyShared = false;

		foreach (var sv in services.OrderByDescending(x => x.Proc?.Ram ?? 0))
		{
			// shared process (svchost): the numbers are for the whole process
			bool shared = sv.Pid != 0 && everyService.Count(x => x.Pid == sv.Pid) > 1;
			if (shared) anyShared = true;

			var u = UsageCells(sv.Proc);

			table.AddRow(
				$"[cyan]{Markup.Escape(sv.Display)}[/]",
				$"[grey]{Markup.Escape(sv.Name)}[/]",
				$"[grey]{(sv.Pid == 0 ? "-" : sv.Pid.ToString())}{(shared ? "*" : "")}[/]",
				u[0], u[1], u[2], u[3], u[4],
				sv.Start == "Auto" ? "[green]Auto[/]" : sv.Start == "Disabled" ? "[red]Disabled[/]" : sv.Start);
		}

		AnsiConsole.Write(table);

		AnsiConsole.MarkupLine($"[grey]{services.Count} running service{(services.Count == 1 ? "" : "s")}, largest RAM use first. CPU is measured over 1 second.[/]");
		if (anyShared)
		{
			AnsiConsole.MarkupLine("[grey]* = runs in a process shared with other services, so RAM and CPU are for the whole process.[/]");
		}
		AnsiConsole.MarkupLine(CpuNote());
	}

	// show apps [filter]
	public void DisplayApps(string filter = "", bool useRegex = false)
	{
		Regex? matcher;
		try
		{
			matcher = filter == "" ? null : Tools.MakeMatcher(filter, useRegex, true);
		}
		catch (ArgumentException ex)
		{
			AnsiConsole.MarkupLine("[red]That is not a valid regular expression:[/] " + Markup.Escape(ex.Message));
			return;
		}

		var all = Sample().Values.ToList();

		// windows: an app is a program with a window. Other systems can't see windows, so there it's
		// any program run by the current user. All processes of a program are added up (chrome)
		var mine = OperatingSystem.IsWindows() ? null : CurrentUserProcessIds();
		if (mine != null)
		{
			all = all.Where(p => mine.Contains(p.Pid)).ToList();
		}

		var apps = new List<ProcInfo>();
		var counts = new Dictionary<string, int>();

		foreach (var name in all.Where(p => mine != null || p.Title != "").Select(p => p.Name).Distinct())
		{
			var group = all.Where(p => p.Name == name).ToList();
			var window = group.Where(p => p.Title != "").OrderByDescending(p => p.Ram).FirstOrDefault() ?? group.OrderByDescending(p => p.Ram).First();

			// filter matches the name or the window title
			if (matcher != null && !Tools.IsMatch(matcher, name) && !Tools.IsMatch(matcher, window.Title))
			{
				continue;
			}

			apps.Add(new ProcInfo(
				window.Pid,
				name,
				group.Sum(p => p.Ram),
				group.Any(p => p.CpuKnown),
				TimeSpan.FromTicks(group.Sum(p => p.CpuTime.Ticks)),
				group.Sum(p => p.Cores),
				group.Sum(p => p.Threads),
				window.Title,
				group.Min(p => p.Started)));
			counts[name] = group.Count;
		}

		if (apps.Count == 0)
		{
			AnsiConsole.MarkupLine(filter == "" ? "[yellow]No apps with a window found.[/]" : "[yellow]No apps match[/] '" + Markup.Escape(filter) + "'.");
			return;
		}

		var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
		table.AddColumn("[yellow]App[/]");
		table.AddColumn("[yellow]Window[/]");
		table.AddColumn(new TableColumn("[yellow]Procs[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]RAM[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]RAM %[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]CPU %[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]CPU time[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]Cores[/]").NoWrap());
		table.AddColumn(new TableColumn("[yellow]Running for[/]").NoWrap());

		// cut the long list on linux/mac
		int maxApps = mine != null ? 30 : apps.Count;

		foreach (var app in apps.OrderByDescending(a => a.Ram).Take(maxApps))
		{
			var u = UsageCells(app);
			string title = app.Title.Length > 36 ? app.Title.Substring(0, 35) + "…" : app.Title;

			table.AddRow(
				$"[cyan]{Markup.Escape(app.Name)}[/]",
				Markup.Escape(title),
				$"[grey]{counts[app.Name]}[/]",
				u[0], u[1], u[2], u[3], u[4],
				app.Started == null ? "-" : FormatAge(DateTime.Now - app.Started.Value));
		}

		AnsiConsole.Write(table);

		long totalRam = apps.Sum(a => a.Ram);
		AnsiConsole.MarkupLine($"[grey]{apps.Count} app{(apps.Count == 1 ? "" : "s")}{(mine != null ? "" : " with a window")}, using {Formatter.FormatSize(totalRam)} of RAM ({totalRam * 100.0 / TotalRam:0.#}%) between them, largest first. CPU is measured over 1 second.[/]");
		AnsiConsole.MarkupLine(CpuNote());

		if (mine != null)
		{
			AnsiConsole.MarkupLine("[grey]Window titles are only available on Windows. Here an app is any program you are running" + (apps.Count > maxApps ? $", and only the largest {maxApps} are shown." : ".") + "[/]");
		}
	}

	// cpu and ram per process straight from windows (same source as Task Manager, no admin needed)
	// Cpu and At are in 100ns units, Ram is private memory
	private record Counter(ulong Cpu, ulong At, ulong Ram);

	[DllImport("ntdll.dll")]
	private static extern int NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int lengthNeeded);

	private static Dictionary<int, Counter> ReadCountersWindows()
	{
		var counters = new Dictionary<int, Counter>();

		// the layout below is 64 bit only
		if (IntPtr.Size != 8)
		{
			return counters;
		}

		int size = 1 << 20; // start with 1 MB, Windows says if it needs more
		IntPtr buffer = Marshal.AllocHGlobal(size);

		try
		{
			int status;
			int needed;

			// 5 = SystemProcessInformation, 0xC0000004 = buffer too small
			while ((status = NtQuerySystemInformation(5, buffer, size, out needed)) == unchecked((int)0xC0000004))
			{
				size = needed + 65536;
				buffer = Marshal.ReAllocHGlobal(buffer, (IntPtr)size);
			}

			if (status != 0)
			{
				return counters; // no figures: the CPU columns will show -
			}

			ulong at = (ulong)(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 10_000_000);

			// records are chained by the offset to the next one
			int offset = 0;
			while (true)
			{
				IntPtr entry = buffer + offset;

				int next = Marshal.ReadInt32(entry, 0);
				long privateRam = Marshal.ReadInt64(entry, 8);   // WorkingSetPrivateSize
				long userTime = Marshal.ReadInt64(entry, 40);
				long kernelTime = Marshal.ReadInt64(entry, 48);
				int pid = (int)Marshal.ReadIntPtr(entry, 80);   // UniqueProcessId

				counters[pid] = new Counter((ulong)(userTime + kernelTime), at, (ulong)privateRam);

				if (next == 0) break;
				offset += next;
			}
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}

		return counters;
	}

	// services from the Windows service manager

	[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint access);

	[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern bool EnumServicesStatusEx(IntPtr manager, int infoLevel, uint serviceType, uint serviceState, IntPtr buffer, uint bufferSize, out uint bytesNeeded, out uint servicesReturned, ref uint resumeHandle, string? groupName);

	[DllImport("advapi32.dll")]
	private static extern bool CloseServiceHandle(IntPtr handle);

	[StructLayout(LayoutKind.Sequential)]
	private struct ServiceEntry
	{
		public IntPtr Name;
		public IntPtr Display;
		public uint Type, State, Controls, ExitCode, SpecificExitCode, CheckPoint, WaitHint, ProcessId, Flags;
	}

	[SupportedOSPlatform("windows")]
	private static List<(string Name, string Display, string Start, int Pid)> ReadServicesWindows()
	{
		var services = new List<(string Name, string Display, string Start, int Pid)>();

		// 4 = SC_MANAGER_ENUMERATE_SERVICE
		IntPtr manager = OpenSCManager(null, null, 4);
		if (manager == IntPtr.Zero)
		{
			throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
		}

		IntPtr buffer = IntPtr.Zero;

		try
		{
			uint bytesNeeded = 0;
			uint count = 0;
			uint resume = 0;

			// first call only returns the buffer size (0x30 = win32 services, 1 = active ones)
			EnumServicesStatusEx(manager, 0, 0x30, 1, IntPtr.Zero, 0, out bytesNeeded, out count, ref resume, null);

			uint size = bytesNeeded + 4096;
			buffer = Marshal.AllocHGlobal((int)size);
			resume = 0;

			if (!EnumServicesStatusEx(manager, 0, 0x30, 1, buffer, size, out bytesNeeded, out count, ref resume, null))
			{
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
			}

			int entrySize = Marshal.SizeOf<ServiceEntry>();

			for (int i = 0; i < count; i++)
			{
				var entry = Marshal.PtrToStructure<ServiceEntry>(buffer + i * entrySize);

				if (entry.State != 4) continue; // 4 = running (the active ones also include starting and stopping)

				string name = Marshal.PtrToStringUni(entry.Name) ?? "";
				string display = Marshal.PtrToStringUni(entry.Display) ?? "";

				// line breaks would wreck the table
				display = Regex.Replace(display, @"\s+", " ").Trim();

				services.Add((name, display, StartType(name), (int)entry.ProcessId));
			}
		}
		finally
		{
			if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
			CloseServiceHandle(manager);
		}

		return services;
	}

	// Auto/Manual/Disabled from the registry
	[SupportedOSPlatform("windows")]
	private static string StartType(string serviceName)
	{
		try
		{
			using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName);

			return (key?.GetValue("Start")) switch
			{
				0 => "Boot",
				1 => "System",
				2 => "Auto",
				3 => "Manual",
				4 => "Disabled",
				_ => "-"
			};
		}
		catch (Exception)
		{
			return "-";
		}
	}

	// all processes, cpu measured over a second
	private static Dictionary<int, ProcInfo> Sample()
	{
		return AnsiConsole.Status().Start("Measuring CPU use (1 second)...", ctx =>
		{
			// two readings a second apart
			var first = ReadCounters();
			Thread.Sleep(1000);
			var second = ReadCounters();

			var processes = Process.GetProcesses();
			var result = new Dictionary<int, ProcInfo>();

			foreach (var p in processes)
			{
				try
				{
					bool cpuKnown = first.ContainsKey(p.Id) && second.ContainsKey(p.Id);
					TimeSpan cpuTime = TimeSpan.Zero;
					double cores = 0;
					long ram = p.WorkingSet64;

					if (cpuKnown)
					{
						var a = first[p.Id];
						var b = second[p.Id];

						double seconds = (b.At - a.At) / 10_000_000.0;
						cores = seconds > 0 ? Math.Max(0, (b.Cpu - a.Cpu) / 10_000_000.0 / seconds) : 0;
						cpuTime = TimeSpan.FromSeconds(b.Cpu / 10_000_000.0);
						if (b.Ram > 0) ram = (long)b.Ram;
					}

					result[p.Id] = new ProcInfo(
						p.Id,
						p.ProcessName,
						ram,
						cpuKnown,
						cpuTime,
						cores,
						TryGet(() => p.Threads.Count, 0),
						TryGet(() => p.MainWindowTitle, ""),
						TryGet<DateTime?>(() => p.StartTime, null));
				}
				catch (Exception)
				{
					// gone while we were looking
				}
				finally
				{
					p.Dispose();
				}
			}

			return result;
		});
	}

	// protected processes throw on some properties
	private static T TryGet<T>(Func<T> read, T fallback)
	{
		try
		{
			return read();
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	// RAM, RAM %, CPU %, CPU time and Cores cells, coloured by load
	private static string[] UsageCells(ProcInfo? p)
	{
		if (p == null)
		{
			return new[] { "-", "-", "-", "-", "-" };
		}

		double ramPercent = p.Ram * 100.0 / TotalRam;
		string ramColour = ramPercent >= 5 ? "red" : ramPercent >= 1 ? "yellow" : "cyan";

		if (!p.CpuKnown)
		{
			return new[] { $"[{ramColour}]{Formatter.FormatSize(p.Ram)}[/]", $"[{ramColour}]{ramPercent:0.0}%[/]", "[grey]-[/]", "[grey]-[/]", "[grey]-[/]" };
		}

		// CPU % is of all cores, Cores is the same thing in cores
		double cpuPercent = p.Cores * 100.0 / Environment.ProcessorCount;
		string cpuColour = cpuPercent >= 25 ? "red" : cpuPercent >= 5 ? "yellow" : "cyan";

		return new[]
		{
			$"[{ramColour}]{Formatter.FormatSize(p.Ram)}[/]",
			$"[{ramColour}]{ramPercent:0.0}%[/]",
			$"[{cpuColour}]{cpuPercent:0.0}%[/]",
			FormatCpuTime(p.CpuTime),
			$"[{cpuColour}]{p.Cores:0.00}[/]"
		};
	}

	private static string CpuNote()
	{
		return $"[grey]CPU % is of all {Environment.ProcessorCount} logical cores. Cores is the same use counted in cores (1.00 = one whole core). CPU time is the total it has used since it started.[/]";
	}

	private static string FormatCpuTime(TimeSpan t)
	{
		if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
		if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds:00}s";
		return $"{t.TotalSeconds:0.0}s";
	}

	private static string FormatAge(TimeSpan t)
	{
		if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
		if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
		return $"{Math.Max(0, (int)t.TotalMinutes)}m";
	}
}
