using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace show
{
	public class Network
	{

		public string GetLocalIPAddress()
		{
			// prefer the adapter with a gateway, that's the one that gets out to the internet
			foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (ni.OperationalStatus != OperationalStatus.Up) continue;

				var props = ni.GetIPProperties();
				bool hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any));
				if (!hasGateway) continue;

				var ip = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
				if (ip != null) return ip.Address.ToString();
			}

			// fallback: whatever the host name resolves to
			var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					return ip.ToString();
				}
			}
			throw new Exception("No network adapters with an IPv4 address in the system!");


		}

		public void DisplayLocalIPAddress()
		{
			try
			{
				var n = new show.Network();
				AnsiConsole.Write(Formatter.FormatIPAddress(n.GetLocalIPAddress()));
				AnsiConsole.Write(Formatter.FormatLocalPingResults(PingCommonDNSServers()));
				AnsiConsole.Write(Formatter.FormatEthernetInterfaces(GetEthernetInterfaces()));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving local IP address: {ex.Message}");
			}
		}


		public List<(string dnsServer, bool pingSuccess, long latency)> PingCommonDNSServers()
		{

			string[] hosts = { "8.8.8.8", "8.8.4.4", "1.1.1.1", "1.0.0.1" }; // Google DNS =

			List<(string dnsServer, bool pingSucess, long latency)> pingResults = new List<(string dnsServer, bool pingSuccess, long latency)>();

			using (Ping ping = new Ping())
			{

				foreach (string host in hosts)
				{

					PingReply reply = ping.Send(host, 1000); // 1000ms timeout



					if (reply.Status == IPStatus.Success)
					{
						//	Console.WriteLine($"Success! Latency: {reply.RoundtripTime}ms");

						pingResults.Add((host, reply.Status == IPStatus.Success, reply.RoundtripTime));


					}
					else
					{
						Console.WriteLine($"Failed: {reply.Status}");

						pingResults.Add((host, false, reply.RoundtripTime));
					}
				}
			}

			return pingResults;


		}

		public List<NetworkInterface> GetEthernetInterfaces()
		{

			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			List<NetworkInterface> ethernetInterfaces = new List<NetworkInterface>();


			foreach (var iface in interfaces)
			{


				if (iface.OperationalStatus == OperationalStatus.Up)
				{

					ethernetInterfaces.Add(iface);

				}


			}

			return ethernetInterfaces;
		}

		public static void GetOpenPorts(string ip)
		{
			try
			{

				using var client = new System.Net.Sockets.TcpClient();
				bool open = client.ConnectAsync("192.168.0.1", 80).Wait(500); // 500 ms timeout

				var props = IPGlobalProperties.GetIPGlobalProperties();

				foreach (var ep in props.GetActiveTcpListeners().OrderBy(e => e.Port))
					Console.WriteLine($"TCP {ep.Address}:{ep.Port}");

				foreach (var ep in props.GetActiveUdpListeners().OrderBy(e => e.Port))
					Console.WriteLine($"UDP {ep.Address}:{ep.Port}");

				foreach (var c in props.GetActiveTcpConnections())
					Console.WriteLine($"{c.LocalEndPoint} -> {c.RemoteEndPoint}  {c.State}");


			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving open ports: {ex.Message}");
			}

		}
	}
}
