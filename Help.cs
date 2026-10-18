using System.Reflection;
using Spectre.Console;

namespace show;

// show help / show --help / show help <command> / show <command> --help
public class Help
{
	// options and examples are { name, description } pairs
	private record Command(string Name, string Summary, string Description, string[] Usage, string[][] Options, string[][] Examples);

	static readonly Command[] Commands =
	{
		new Command("file",
			"Show details of one file",
			"Shows a table with the name, size, created / last access / last write dates, attributes and extension of a file.",
			new[] { "show file <path>" },
			new[] { new[] { "<path>", "The file to look at. A relative path starts from the folder you run show in." } },
			new[] { new[] { "show file notes.txt", "details of notes.txt" },
					new[] { "show file \"C:\\My Files\\report.docx\"", "put quotes around paths with spaces" } }),

		new Command("dir",
			"Show a folder, its subfolders and their sizes",
			"Shows the folder's size, file count and dates, then a table of its subfolders. A coloured bar below shows how much of the folder each subfolder takes up. " +
			"Files sitting directly in the folder are shown as 'This Folder [name]'. Sizes include everything inside a subfolder; the Files column counts only the files directly in it.",
			new[] { "show dir <path>" },
			new[] { new[] { "<path>", "The folder to look at. Use . for the current folder." } },
			new[] { new[] { "show dir .", "the folder you are in" },
					new[] { "show dir C:\\Users", "a folder by full path" } }),

		new Command("csv",
			"Show a CSV file as a table",
			"Prints a CSV file as a table with a row number column. If the first row looks like a header (all text, no empty cells, no numbers or dates, no repeats) its values become the column names; " +
			"otherwise the columns are called Col 1, Col 2 ... and the first row is treated as data. A line under the table says which was detected.",
			new[] { "show csv <file>", "show csv <file> head [count]", "show csv <file> tail [count]" },
			new[] { new[] { "<file>", "The CSV file to show." },
					new[] { "head", "Only the first rows." },
					new[] { "tail", "Only the last rows. Row numbers still match the file." },
					new[] { "[count]", "How many rows for head / tail. Default 10." } },
			new[] { new[] { "show csv people.csv", "every row" },
					new[] { "show csv people.csv head", "the first 10 rows" },
					new[] { "show csv people.csv tail 5", "the last 5 rows" } }),

		new Command("code",
			"Show a code file with syntax highlighting",
			"Prints a source file with colours and line numbers, using the VS Code Dark+ theme. The language is chosen from the file extension (.cs, .py, .js ...). " +
			"A file type with no grammar is not shown.",
			new[] { "show code <file>", "show code <file> head [count]", "show code <file> tail [count]" },
			new[] { new[] { "<file>", "The code file to show." },
					new[] { "head", "Only the first lines." },
					new[] { "tail", "Only the last lines. With -n the line numbers still match the file." },
					new[] { "[count]", "How many lines for head / tail. Default 20." },
					new[] { "-n, --lines", "Show line numbers. They are off by default." } },
			new[] { new[] { "show code Program.cs", "the whole file" },
					new[] { "show code Program.cs head 30", "the first 30 lines" },
					new[] { "show code Program.cs tail", "the last 20 lines" },
					new[] { "show code Program.cs -n", "with line numbers" } }),

		new Command("json",
			"Make JSON easy to read",
			"Checks that JSON is valid and, if it is not neatly formatted, rewrites it with line breaks and indents. The result is shown with syntax highlighting and line numbers. " +
			"The input can be a file, JSON text typed in quotes, or text piped in. Comments and trailing commas are accepted and the comments are dropped when it is reformatted. " +
			"If the JSON is not valid, the problem is reported with its line and column and the original text is shown so it can be fixed.",
			new[] { "show json <file>", "show json \"<json text>\"", "<command> | show json", "show json <file> head [count]", "show json <file> tail [count]" },
			new[] { new[] { "<file>", "A JSON file. Anything that is not an existing file is treated as JSON text." },
					new[] { "head", "Only the first lines of the result." },
					new[] { "tail", "Only the last lines of the result." },
					new[] { "[count]", "How many lines for head / tail. Default 20." },
					new[] { "-n, --lines", "Show line numbers. They are off by default." } },
			new[] { new[] { "show json data.json", "read and format a file" },
					new[] { "show json '{\"a\":1,\"b\":[2,3]}'", "format text typed on the command line" },
					new[] { "type data.json | show json", "format piped text" } }),

		new Command("xml",
			"Make XML easy to read",
			"Checks that XML is valid and, if it is not neatly formatted, rewrites it with line breaks and indents (2 spaces). The result is shown with syntax highlighting and line numbers. " +
			"The input can be a file, XML text typed in quotes, or text piped in. If the XML is not valid, the problem is reported with its line and column and the original text is shown. " +
			"Spaces between tags are not kept, so mixed text-and-tag content can change slightly.",
			new[] { "show xml <file>", "show xml \"<xml text>\"", "<command> | show xml", "show xml <file> head [count]", "show xml <file> tail [count]" },
			new[] { new[] { "<file>", "An XML file. Anything that is not an existing file is treated as XML text." },
					new[] { "head", "Only the first lines of the result." },
					new[] { "tail", "Only the last lines of the result." },
					new[] { "[count]", "How many lines for head / tail. Default 20." },
					new[] { "-n, --lines", "Show line numbers. They are off by default." } },
			new[] { new[] { "show xml settings.xml", "read and format a file" },
					new[] { "show xml settings.xml head 30", "the first 30 lines" },
					new[] { "type settings.xml | show xml", "format piped text" } }),

		new Command("xaml",
			"Make XAML easy to read",
			"The same as xml, but laid out the way XAML is usually written: 4 space indents and every attribute of an element on its own line. An element with only one attribute stays on one line.",
			new[] { "show xaml <file>", "show xaml \"<xaml text>\"", "<command> | show xaml", "show xaml <file> head [count]", "show xaml <file> tail [count]" },
			new[] { new[] { "<file>", "A XAML file. Anything that is not an existing file is treated as XAML text." },
					new[] { "head", "Only the first lines of the result." },
					new[] { "tail", "Only the last lines of the result." },
					new[] { "[count]", "How many lines for head / tail. Default 20." },
					new[] { "-n, --lines", "Show line numbers. They are off by default." } },
			new[] { new[] { "show xaml MainWindow.xaml", "read and format a file" },
					new[] { "show xaml MainWindow.xaml tail 15", "the last 15 lines" } }),

		new Command("locate",
			"Find files, text inside files, or folders",
			"Searches a folder and every folder inside it. Three kinds of search: file finds files by name, text finds files that contain some text and shows the matching lines (with the match highlighted), " +
			"directory lists the folders. Without a start path the search starts in the current folder. A word counts as a path if it has a \\ / or : in it, or is . or .. (so write .\\src, not src); any other word is a name. Folders you are not allowed to open are skipped, and upper / lower case is ignored. " +
			"Names and text can use * (any text) and ? (one character). A name with no wildcard matches anywhere in the name, and text is found anywhere in a line. " +
			"Add -r to use a regular expression instead. Text search skips programs (binary files), files over 20 MB, and .git and node_modules folders.",
			new[] { "show locate file <filename> [ext] [path] [filter]", "show locate text <text> [path] [ext]", "show locate directory [path] [pattern]" },
			new[] { new[] { "<filename>", "File search: the name to look for. Use * for any name." },
					new[] { "<text>", "Text search: the text to look for." },
					new[] { "[path]", "The folder to start in. Without it the search starts in the current folder." },
					new[] { "[ext]", "Only files with this extension, with or without the dot (cs or .cs)." },
					new[] { "[filter]", "File search: only keep files whose full path contains this text." },
					new[] { "[pattern]", "Directory search: only folders whose name matches." },
					new[] { "-r, --regex", "Treat the name, text or pattern as a regular expression." } },
			new[] { new[] { "show locate file report txt", "files with report in the name, .txt only" },
					new[] { "show locate file \"inv*_2024\" pdf D:\\docs", "wildcards, in a chosen folder" },
					new[] { "show locate file \"^report_\\d+\" -r", "regular expression: report_ followed by digits" },
					new[] { "show locate text TODO D:\\code cs", "lines containing TODO in .cs files" },
					new[] { "show locate text \"TODO*fix\" D:\\code", "TODO followed later by fix on the same line" },
					new[] { "show locate text \"v\\d+\\.\\d+\" -r", "lines with a version number like v1.2" },
					new[] { "show locate directory D:\\code", "every folder below D:\\code" },
					new[] { "show locate directory D:\\code \"test*\"", "folders whose name starts with test" } }),

		new Command("drives",
			"Show every drive and how full it is",
			"Shows a table with each ready drive's type, file system, total size, used space, free space and available space, followed by a bar comparing the drives. Drives that are not ready are listed as such.",
			new[] { "show drives" },
			new string[0][],
			new[] { new[] { "show drives", "all drives" } }),

		new Command("pc",
			"Show PC specs and hardware",
			"Shows tables of hardware and system information. Windows gives the most detail. On Linux it is read from /proc, /sys and programs such as lsblk and lspci, and macOS shows less. " +
			"Anything that can't be found shows - or a short note, for example the motherboard in a virtual machine.",
			new[] { "show pc", "show pc <section>" },
			new[] { new[] { "all", "Every section. This is the default." },
					new[] { "os", "Operating system, version, build, computer name." },
					new[] { "cpu", "Processor name, cores, threads, maximum speed." },
					new[] { "gpu", "Graphics cards, driver version, resolution." },
					new[] { "ram", "Total memory and each installed memory stick." },
					new[] { "board", "Motherboard maker, model and BIOS version." },
					new[] { "disk", "Physical disks: model, size, interface, type." } },
			new[] { new[] { "show pc", "everything" },
					new[] { "show pc gpu", "only the graphics cards" },
					new[] { "show pc cpu", "only the processor" } }),

		new Command("services",
			"Show running Windows services and what they use",
			"Lists the services that are running with the memory and CPU used by the process each one runs in, sorted with the largest RAM use first. " +
			"RAM is the process's private memory (what Task Manager shows as Memory), RAM % is its share of the PC's memory. CPU % is its share of the whole CPU measured over one second, " +
			"Cores is the same use counted in cores (1.00 = one whole core) and CPU time is the total CPU it has used since it started. " +
			"Several services can share one process (svchost); those are marked with * and show the numbers for the whole process. " +
			"Some protected processes can't be measured and show -. Works on Windows, Linux (systemd services) and macOS (launchd). " +
			"On Linux and macOS the RAM is the resident memory of the process and the start type comes from systemd.",
			new[] { "show services", "show services <filter>" },
			new[] { new[] { "<filter>", "Optional. Matches the service name, its short name or the program it runs in. Use * and ? as wildcards. Without a wildcard it matches anywhere in the name." },
					new[] { "-r, --regex", "Treat the filter as a regular expression." } },
			new[] { new[] { "show services", "every running service" },
					new[] { "show services sql", "services with sql in the name" },
					new[] { "show services svchost", "services that run inside svchost" },
					new[] { "show services \"^Windows (Update|Time)\" -r", "regular expression" } }),

		new Command("apps",
			"Show open apps and what they use",
			"Lists the programs that have a window open with the memory and CPU they use, largest RAM first. A program with many processes (a browser, for example) is added together " +
			"into one row, and Procs says how many. Columns: the main window's title, RAM, RAM %, CPU %, CPU time, Cores and how long the program has been running. " +
			"The CPU numbers are measured over one second and are explained under the table. " +
			"Windows can tell which programs have a window. Linux and macOS can't, so there an app is any program you are running (the largest 30 are shown) and the Window column is empty.",
			new[] { "show apps", "show apps <filter>" },
			new[] { new[] { "<filter>", "Optional. Matches the program name or its window title. Use * and ? as wildcards. Without a wildcard it matches anywhere in the name." },
					new[] { "-r, --regex", "Treat the filter as a regular expression." } },
			new[] { new[] { "show apps", "every open app" },
					new[] { "show apps chrome", "just the browser" },
					new[] { "show apps \"^(code|devenv)$\" -r", "Visual Studio Code or Visual Studio" } }),

		new Command("ip",
			"Show network information",
			"Shows this PC's IPv4 address, pings four public DNS servers (8.8.8.8, 8.8.4.4, 1.1.1.1, 1.0.0.1) with their response time, and lists the network interfaces that are up with their MAC address, IP address and speed.",
			new[] { "show ip" },
			new string[0][],
			new[] { new[] { "show ip", "address, DNS check and network interfaces" } }),

		new Command("ports",
			"Show open (listening) ports and who uses them",
			"Shows every port something is listening on, with the program and Windows service using it, its PID and the address it is bound to. " +
			"The address is yellow when the port is reachable from any network, green when only this PC can reach it. On Linux and macOS the program is only known for your own ports unless you run show with sudo. Ports that are used by the same program in a row of 5 or more are shown as one range.",
			new[] { "show ports", "show ports <port>", "show ports <name>" },
			new[] { new[] { "<port>", "A port number. Says whether it is open, or that it is closed." },
					new[] { "<name>", "Part of a program or service name. Shows only its ports." } },
			new[] { new[] { "show ports", "every open port" },
					new[] { "show ports 443", "is port 443 open, and who is using it" },
					new[] { "show ports chrome", "ports used by Chrome" } }),

		new Command("containers",
			"Show Docker containers",
			"Shows every Docker container, running or stopped, with its image, state, status, published ports, number of mounts and short ID. Docker (Docker Desktop on Windows and macOS) must be running. Set the DOCKER_HOST environment variable if it is somewhere else. " +
			"The filter is matched against the name, the image and the ID. * means any text and ? means one character. A filter without a wildcard matches anywhere in the name, so postgres works like *postgres*. Matching ignores upper / lower case.",
			new[] { "show containers", "show containers <filter>" },
			new[] { new[] { "<filter>", "Optional. Use * and ? as wildcards. Put it in quotes in Git Bash." } },
			new[] { new[] { "show containers", "all containers" },
					new[] { "show containers *postgres*", "containers named or built from postgres" },
					new[] { "show containers lite*", "names starting with lite" } }),

		new Command("help",
			"Show this help",
			"Lists the commands, or shows the full manual for one command.",
			new[] { "show help", "show help <command>", "show <command> --help" },
			new[] { new[] { "<command>", "The command to explain." } },
			new[] { new[] { "show help", "the list of commands" },
					new[] { "show help csv", "the manual for csv" },
					new[] { "show ports --help", "the manual for ports" } })
	};

	public void Display(string topic = "")
	{
		if (topic == "")
		{
			ShowOverview();
			return;
		}

		var command = Commands.FirstOrDefault(c => c.Name == topic.ToLower());

		if (command == null)
		{
			AnsiConsole.MarkupLine($"[red]There is no command called '{Markup.Escape(topic)}'.[/]");
			AnsiConsole.WriteLine();
			ShowOverview();
			return;
		}

		ShowManual(command);
	}

	private void ShowOverview()
	{
		var assembly = Assembly.GetExecutingAssembly();
		string version = assembly.GetName().Version?.ToString() ?? "Unknown";
		string description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

		AnsiConsole.Write(new Rule($"[yellow]show[/] [grey]version {Markup.Escape(version)}[/]").LeftJustified().RuleStyle("grey"));
		if (description != "")
		{
			AnsiConsole.MarkupLine(Markup.Escape(description));
		}

		AnsiConsole.MarkupLine("\n[yellow]USAGE[/]");
		AnsiConsole.MarkupLine("  show [cyan]<command>[/] [grey][[options]][/]");
		AnsiConsole.WriteLine();

		var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
		table.AddColumn("[yellow]Command[/]");
		table.AddColumn("[yellow]What it does[/]");
		table.AddColumn("[yellow]Example[/]");

		foreach (var c in Commands)
		{
			table.AddRow($"[cyan]{c.Name}[/]", Markup.Escape(c.Summary), $"[grey]{Markup.Escape(c.Examples[0][0])}[/]");
		}

		AnsiConsole.Write(table);

		AnsiConsole.MarkupLine("Run [cyan]show help <command>[/] for the full manual of a command.");
		AnsiConsole.MarkupLine("[grey]You can chain commands, e.g. [/][cyan]show drives ip[/]");
	}

	private void ShowManual(Command c)
	{
		AnsiConsole.Write(new Rule($"[yellow]show {c.Name}[/]").LeftJustified().RuleStyle("grey"));
		AnsiConsole.MarkupLine(Markup.Escape(c.Description));

		AnsiConsole.MarkupLine("\n[yellow]USAGE[/]");
		foreach (var usage in c.Usage)
		{
			AnsiConsole.MarkupLine("  [cyan]" + Markup.Escape(usage) + "[/]");
		}

		if (c.Options.Length > 0)
		{
			AnsiConsole.MarkupLine("\n[yellow]ARGUMENTS[/]");
			AnsiConsole.Write(new Padder(TwoColumns(c.Options, "cyan", "white"), new Padding(2, 0, 0, 0)));
		}

		AnsiConsole.MarkupLine("\n[yellow]EXAMPLES[/]");
		AnsiConsole.Write(new Padder(TwoColumns(c.Examples, "green", "grey"), new Padding(2, 0, 0, 0)));
	}

	// two columns, no border
	private static Grid TwoColumns(string[][] rows, string leftColour, string rightColour)
	{
		var grid = new Grid();
		grid.AddColumn(new GridColumn().NoWrap().PadRight(3));
		grid.AddColumn();

		foreach (var row in rows)
		{
			grid.AddRow(
				new Markup($"[{leftColour}]{Markup.Escape(row[0])}[/]"),
				new Markup($"[{rightColour}]{Markup.Escape(row[1])}[/]"));
		}

		return grid;
	}
}
