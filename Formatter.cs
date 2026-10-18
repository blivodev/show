using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;
using System.IO;
using System.Reflection;
using System.Net.NetworkInformation;

namespace show
{
	public static class Formatter
	{

		public static int ColourRandomizer(){

			Random random = new Random();
			int i = 0;

			i = random.Next(1, 290);


			return i;
		}


		public static Table FormatFileInfo(FileInfo fileInfo)
		{
			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

			if (fileInfo == null)
			{
				Console.WriteLine("File information is not available.");
			}
			else
			{

				table.AddColumn("[yellow]Property[/]");
				table.AddColumn("[cyan]Value[/]");
				table.AddRow("[yellow]Name[/]", $"[cyan]{Markup.Escape(fileInfo.Name)}[/]");
				table.AddRow("[yellow]Size[/]", $"[cyan]{fileInfo.Length} bytes[/]");
				table.AddRow("[yellow]Created[/]", $"[cyan]{fileInfo.CreationTime}[/]");
				table.AddRow("[yellow]Last Access[/]", $"[cyan]{fileInfo.LastAccessTime}[/]");
				table.AddRow("[yellow]Last Write[/]", $"[cyan]{fileInfo.LastWriteTime}[/]");
				table.AddRow("[yellow]Attributes[/]", $"[cyan]{fileInfo.Attributes}[/]");
				// no unix permissions on windows
				if (!OperatingSystem.IsWindows())
				{
					table.AddRow("[yellow]File Mode[/]", $"[cyan]{fileInfo.UnixFileMode}[/]");
				}
				table.AddRow("[yellow]Extension[/]", $"[cyan]{Markup.Escape(fileInfo.Extension)}[/]");
			}

			return table;
		}

		public static Table FormatDirectoryInfo(DirectoryInfo dirInfo, long dirSize)
		{

			var dirTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

			if (dirInfo == null)
			{
				Console.WriteLine("Directory information is not available or path is not a directory.");
			}
			else
			{

				dirTable.AddColumn("[yellow]Directory Name[/]");
				dirTable.AddColumn("[cyan]Size[/]");
				dirTable.AddColumn("[yellow]Files[/]");
				dirTable.AddColumn("[cyan]Created[/]");
				dirTable.AddColumn("[yellow]Last Access[/]");
				dirTable.AddColumn("[cyan]Last Write[/]");
				dirTable.AddRow(
					$"[yellow]{Markup.Escape(dirInfo.Name)}[/]",
					$"[cyan]{dirSize} MB[/]",
					$"[yellow]{dirInfo.GetFiles().Length}[/]",
					$"[cyan]{dirInfo.CreationTime}[/]",
					$"[yellow]{dirInfo.LastAccessTime}[/]",
					$"[cyan]{dirInfo.LastWriteTime}[/]");

			}

			return dirTable;
		}

		public static Table FormatSubDirectories(DirectoryInfo[] dirInfo, long[] sizes)
		{

			var subdirTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

			if (dirInfo == null)
			{
				Console.WriteLine("Directory information is not available or path is not a directory.");
			}
			else
			{

				Console.ForegroundColor = ConsoleColor.Cyan;

				Console.WriteLine($"Folder {dirInfo[0].Parent.Name} contains {dirInfo.Length} sub-directories.");

				Console.ResetColor();

				subdirTable.AddColumn("[orange1]Sub-Directory[/]");
				subdirTable.AddColumn("[PaleGreen1]Size[/]");
				subdirTable.AddColumn("[orange1]Files[/]");
				subdirTable.AddColumn("[PaleGreen1]Created[/]");
				subdirTable.AddColumn("[orange1]Last Access[/]");
				subdirTable.AddColumn("[PaleGreen1]Last Write[/]");


				for (int i = 0; i < dirInfo.Length; i++)
				{
					long sizeMb = sizes[i] / 1024 / 1024;

					subdirTable.AddRow(
						$"[orange1]{Markup.Escape(dirInfo[i].Name)}[/]",
						$"[PaleGreen1]{sizeMb} MB[/]",
						$"[orange1]{dirInfo[i].GetFiles().Length}[/]",
						$"[PaleGreen1]{dirInfo[i].CreationTime}[/]",
						$"[orange1]{dirInfo[i].LastAccessTime}[/]",
						$"[PaleGreen1]{dirInfo[i].LastWriteTime}[/]");
				}


			}

			return subdirTable;
		}

		public static BreakdownChart VisualizeDirectorySize(DirectoryInfo[] dirInfo, long[] sizes, long rootSize)
		{

			var chart = new BreakdownChart().Width(80);
			chart.UseValueFormatter(bytes => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024:0.#} MB" : $"{bytes / 1024:0.#} KB");

			var colors = ColorPalette.Pick(dirInfo.Length);
			long subTotal = 0;

			for (int i = 0; i < dirInfo.Length; i++)
			{
				chart.AddItem(dirInfo[i].Name, sizes[i], colors[i]);
				subTotal += sizes[i];
			}

			long rest = rootSize - subTotal;
			if (rest > 0)
			{
				chart.AddItem("This Folder [" + dirInfo[0].Parent.Name + "]", rest, Color.Grey);
			}

			return chart;

		}


		public static string FormatSize(long bytes)
		{
			string[] units = { "B", "KB", "MB", "GB", "TB" };
			double size = bytes;
			int u = 0;

			while (size >= 1024 && u < units.Length - 1)
			{
				size /= 1024;
				u++;
			}

			return $"{size:0.#} {units[u]}";
		}

		// each row is coloured like its chart segment
		public static Table FormatDriveInfo(DriveInfo[] drives, List<Color> colors)
		{
			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

			table.AddColumn("Drive");
			table.AddColumn("Type");
			table.AddColumn("Format");
			table.AddColumn("Total");
			table.AddColumn("Used");
			table.AddColumn("Free");
			table.AddColumn("Available");

			for (int i = 0; i < drives.Length; i++)
			{
				DriveInfo d = drives[i];
				Color c = colors[i];
				string colour = $"rgb({c.R},{c.G},{c.B})";

				long used = d.TotalSize - d.TotalFreeSpace;
				double usedPercent = d.TotalSize == 0 ? 0 : used * 100.0 / d.TotalSize;

				table.AddRow(
					$"[{colour}]{Markup.Escape(d.Name.TrimEnd('\\', ':'))}[/]",
					$"[{colour}]{d.DriveType}[/]",
					$"[{colour}]{Markup.Escape(d.DriveFormat)}[/]",
					$"[{colour}]{FormatSize(d.TotalSize)}[/]",
					$"[{colour}]{FormatSize(used)} ({usedPercent:0}%)[/]",
					$"[{colour}]{FormatSize(d.TotalFreeSpace)}[/]",
					$"[{colour}]{FormatSize(d.AvailableFreeSpace)}[/]");
			}

			return table;
		}

		public static BreakdownChart VisualizeDriveSize(DriveInfo[] drives, List<Color> colors)
		{

			var chart = new BreakdownChart().Width(80);
			chart.UseValueFormatter(bytes => FormatSize((long)bytes));

			for (int i = 0; i < drives.Length; i++)
			{
				chart.AddItem(drives[i].Name, drives[i].TotalSize, colors[i]);
			}

			return chart;

		}

		public static Table FormatIPAddress(string ipAddress)
		{

			var n = new show.Network();


			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
			if (string.IsNullOrEmpty(ipAddress))
			{
				Console.WriteLine("IP address is not available.");
			}
			else
			{
				//table.AddColumn("[yellow]Local IP[/]");
				//table.AddColumn("[cyan]Value[/]");
				table.AddColumn("[yellow]Local IP Address[/]");
				table.AddColumn($"[cyan]{Markup.Escape(n.GetLocalIPAddress())}[/]");
			//	table.AddRow(, $"[cyan]{Markup.Escape(n.GetLocalIPAddress())}[/]");

			}



			return table;
		}

		public static Grid FormatLocalPingResults(List<(string dnsServer, bool pingSuccess, long latency)> pingResults)
		{
			var grid = new Grid();

			grid.AddColumn();
			grid.AddColumn();
			grid.AddColumn();

			grid.AddRow("[yellow]DNS Server[/]", "[yellow]Status[/]", "[yellow]Latency[/]");

			foreach (var pingResult in pingResults)
			{
				grid.AddRow(
					$"[cyan]{pingResult.dnsServer}[/]",
					pingResult.pingSuccess ? "[green]OK[/]" : "[red]Failed[/]",
					pingResult.pingSuccess ? $"[green]{pingResult.latency} ms[/]" : "[red]-[/]");
			}

			return grid;
		}

		public static Table FormatEthernetInterfaces(List<NetworkInterface> interfaces)
		{
			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

			table.AddColumn("[yellow]Interface Name[/]");
			table.AddColumn("[blue]MAC Address[/]");
			table.AddColumn("[cyan]IP Address[/]");
			table.AddColumn("[yellow]Status[/]");
			table.AddColumn("[yellow]Speed[/]");

			foreach (var ni in interfaces)
			{
				string mac = string.Join("-", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

				var ips = ni.GetIPProperties().UnicastAddresses
					.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
					.Select(a => a.Address.ToString());
				string ip = string.Join(", ", ips);

				table.AddRow(
					$"[yellow]{Markup.Escape(ni.Name)}[/]",
					$"[blue]{(mac == "" ? "-" : mac)}[/]",
					$"[cyan]{(ip == "" ? "-" : ip)}[/]",
					ni.OperationalStatus == OperationalStatus.Up ? "[green]Up[/]" : "[red]Down[/]",
					$"[yellow]{(ni.Speed > 0 ? ni.Speed / 1_000_000 + " Mbps" : "-")}[/]");
			}

			return table;
		}

		public static void DisplayShowInfo()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var version = assembly.GetName().Version?.ToString() ?? "Unknown";
			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
			var figlet = new FigletText("show");
			//table.AddColumn("");
		///	table.AddColumn("");
		//	table.AddRow("[yellow]Application Name[/]", $"[cyan]{assembly.GetName().Name}[/]", "[yellow]Version[/]", $"[cyan]{version}[/]");
			//table.AddRow("[yellow]Version[/]", $"[cyan]{version}[/]");
			AnsiConsole.Write(figlet);

		//	Console.WriteLine($"Application Name: {assembly.GetName().Name}");
			Console.WriteLine($"Version: {version}");
			Console.WriteLine($"Author: {assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Unknown"}");
			Console.WriteLine($"Description: {assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "No description available."}");
			//assembly.

			//AnsiConsole.Write(table);
		}


		public static void FormatCSV(Table table){

			AnsiConsole.Write(table);


		}



	}
}
