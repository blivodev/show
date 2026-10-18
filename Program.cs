using show;

var f = new show.File();
var d = new show.Directory();
var drives = new show.Drive();
var tools = new show.Tools();


// -n / --lines: line numbers for code, json, xml, xaml. Can go anywhere, so strip it first
bool lineNumbers = args.Contains("-n") || args.Contains("--lines");
args = args.Where(a => a != "-n" && a != "--lines").ToArray();

// -r / --regex: locate patterns are regexes instead of wildcards
bool useRegex = args.Contains("-r") || args.Contains("--regex");
args = args.Where(a => a != "-r" && a != "--regex").ToArray();

// a word with a slash or colon (or . / ..) is a path, anything else is a name
bool LooksLikeFolder(string word) => word.Contains('\\') || word.Contains('/') || word.Contains(':') || word == "." || word == "..";

// no args: banner and version
if (args.Length == 0)
{
	Formatter.DisplayShowInfo();
	Spectre.Console.AnsiConsole.MarkupLine("\nRun [cyan]show help[/] for a list of commands.");
	return;
}

// show help [command], show <command> --help
if (args[0] is "help" or "--help" or "-h" or "/?")
{
	new Help().Display(args.Length > 1 ? args[1] : "");
	return;
}

if (args.Skip(1).Any(a => a is "--help" or "-h" or "/?"))
{
	new Help().Display(args[0]);
	return;
}

for (int i = 0; i < args.Length; i++)
{
	switch (args[i])
	{
		case "files":

			break;
		case "file":
			if (i + 1 < args.Length)
			{
				f.Info(args[i + 1]);
				i++; // skip the filename so it isn't treated as a command
			}
			break;
		case "dir":
			if (i + 1 < args.Length)
			{
				d.Info(args[i + 1]);
				i++; // skip the directory name so it isn't treated as a command
			}
			break;
		case "csv":
			if (i + 1 < args.Length)
			{
				// show csv <file> [head|tail] [count]
				string csvFile = args[i + 1];
				string csvMode = "all";
				int csvCount = 10;
				i++; // skip the file name so it isn't treated as a command

				if (i + 1 < args.Length && (args[i + 1] == "head" || args[i + 1] == "tail"))
				{
					csvMode = args[i + 1];
					i++;

					if (i + 1 < args.Length && int.TryParse(args[i + 1], out int csvRows))
					{
						csvCount = csvRows;
						i++;
					}
				}

				tools.DisplayCSV(csvFile, csvMode, csvCount);
			}
			break;
		case "code":
			if (i + 1 < args.Length)
			{
				// show code <file> [head|tail] [count]
				string codeFile = args[i + 1];
				string codeMode = "all";
				int codeCount = 20;
				i++; // skip the file name so it isn't treated as a command

				if (i + 1 < args.Length && (args[i + 1] == "head" || args[i + 1] == "tail"))
				{
					codeMode = args[i + 1];
					i++;

					if (i + 1 < args.Length && int.TryParse(args[i + 1], out int codeLines))
					{
						codeCount = codeLines;
						i++;
					}
				}

				tools.DisplayCode(codeFile, codeMode, codeCount, lineNumbers);
			}
			break;
		case "json":
		case "xml":
		case "xaml":
			// show json|xml|xaml <file or text> [head|tail] [count], or pipe text in
			string docCommand = args[i];
			string docInput = "";
			string docMode = "all";
			int docCount = 20;

			// with piped text, head/tail aren't the input
			bool hasInput = i + 1 < args.Length && !(Console.IsInputRedirected && args[i + 1] is "head" or "tail");

			if (hasInput)
			{
				docInput = args[i + 1];
				i++; // skip the input so it isn't treated as a command
			}
			else if (Console.IsInputRedirected)
			{
				docInput = Console.In.ReadToEnd();
			}
			else
			{
				Console.WriteLine($"Usage: show {docCommand} <file or text>");
				break;
			}

			if (i + 1 < args.Length && (args[i + 1] == "head" || args[i + 1] == "tail"))
			{
				docMode = args[i + 1];
				i++;

				if (i + 1 < args.Length && int.TryParse(args[i + 1], out int docLines))
				{
					docCount = docLines;
					i++;
				}
			}

			if (docCommand == "json")
			{
				tools.DisplayJson(docInput, docMode, docCount, lineNumbers);
			}
			else
			{
				tools.DisplayXml(docInput, docMode, docCount, docCommand == "xaml", lineNumbers);
			}
			break;
		case "locate":
			// show locate file <name> [ext] [path] [filter]
			// show locate text <text> [path] [ext]
			// show locate directory [path] [pattern]
			string locateMode = i + 1 < args.Length ? args[i + 1] : "";
			bool locateNeedsName = locateMode == "file" || locateMode == "text";

			if ((!locateNeedsName && locateMode != "directory") || (locateNeedsName && i + 2 >= args.Length))
			{
				Console.WriteLine("Usage: show locate file <filename> [ext] [path] [filter]");
				Console.WriteLine("       show locate text <text> [path] [ext]");
				Console.WriteLine("       show locate directory [path] [pattern]");
				Console.WriteLine("       add -r to use a regular expression instead of * and ? wildcards");
				break;
			}

			string locateName = "";   // the file name, the text to look for, or the folder name pattern
			string locateExt = "";
			string locatePath = "";
			string locateFilter = "";
			i++; // skip the mode so it isn't treated as a command

			if (locateNeedsName)
			{
				locateName = args[i + 1];
				i++; // skip the name so it isn't treated as a command
			}

			// what follows is a path, an extension and (file only) a filter, all optional
			int locateWords = locateMode == "file" ? 3 : 2;
			foreach (var word in args.Skip(i + 1).Take(locateWords))
			{
				bool isFolder = LooksLikeFolder(word);

				if (isFolder && locatePath == "")
				{
					locatePath = word;
				}
				else if (locateMode == "directory")
				{
					locateName = word;    // for directory the other word is the name pattern
				}
				else if (!isFolder && locateExt == "")
				{
					locateExt = word;
				}
				else
				{
					locateFilter = word;
				}
				i++;
			}

			if (locateMode == "file")
			{
				tools.DisplayLocate(locateName, locateExt, locatePath, locateFilter, useRegex);
			}
			else if (locateMode == "text")
			{
				tools.DisplayLocateText(locateName, locatePath, locateExt, useRegex);
			}
			else
			{
				tools.DisplayLocateDirectory(locatePath, locateName, useRegex);
			}
			break;
		case "drives":
			drives.DisplayDrives();
			break;
		case "containers":
			// show containers [filter]
			string containerFilter = "";
			if (i + 1 < args.Length)
			{
				containerFilter = args[i + 1];
				i++; // skip the filter so it isn't treated as a command
			}
			await tools.DisplayContainers(containerFilter);
			break;
		case "services":
		case "apps":
			// show services|apps [filter]
			string processCommand = args[i];
			string processFilter = "";
			if (i + 1 < args.Length)
			{
				processFilter = args[i + 1];
				i++; // skip the filter so it isn't treated as a command
			}

			if (processCommand == "services")
			{
				new Pc().DisplayServices(processFilter, useRegex);
			}
			else
			{
				new Pc().DisplayApps(processFilter, useRegex);
			}
			break;
		case "ports":
			// show ports [port or name]
			string portFilter = "";
			if (i + 1 < args.Length)
			{
				portFilter = args[i + 1];
				i++; // skip the filter so it isn't treated as a command
			}
			new Ports().Display(portFilter);
			break;
		case "ip":
			Network n = new Network();
			n.DisplayLocalIPAddress();
			break;
		case "net":
			if (i + 1 < args.Length)
			{
				d.Info(args[i + 1]);
				i++; // skip the directory name so it isn't treated as a command
			}
			break;
		case "pc":
			// show pc [section], all by default
			string section = "all";
			if (i + 1 < args.Length)
			{
				section = args[i + 1];
				i++; // skip the section name so it isn't treated as a command
			}
			new Pc().Display(section);
			break;
		default:
			Console.WriteLine($"Unknown command: {args[i]}. Run 'show help' for a list of commands.");
			break;
	}
}


