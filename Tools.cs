using CsvHelper;
using CsvHelper.Configuration;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using TextMateSharp.Grammars;
using System.IO;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Xml;
using System.Xml.Linq;
using System.Linq;



namespace show
{
	public class Tools
	{


		// summary printed under the csv table
		private string summary = "";

		// mode is all, head or tail. count only matters for head/tail
		public Table ParseCSV(string filePath, string mode = "all", int count = 10)
		{
			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

			// no header handling in the parser, we guess it ourselves
			var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };

			using var reader = new StreamReader(filePath);
			using var parser = new CsvParser(reader, config);

			if (!parser.Read())
			{
				table.AddColumn("Empty file");
				return table;
			}

			string[] firstRow = parser.Record!.ToArray();
			bool hasHeader = LooksLikeHeader(firstRow);
			int columns = firstRow.Length;

			var rows = new List<string[]>(); // the rows we will show
			long total = 0;                  // all data rows in the file

			// keep or drop the row depending on head/tail
			void Add(string[] row)
			{
				total++;

				if (mode == "head")
				{
					if (rows.Count < count) rows.Add(row);
				}
				else if (mode == "tail")
				{
					rows.Add(row);
					if (rows.Count > count) rows.RemoveAt(0);
				}
				else
				{
					rows.Add(row);
				}
			}

			if (!hasHeader)
			{
				Add(firstRow);
			}

			while (parser.Read())
			{
				Add(parser.Record!.ToArray());
			}

			// header names, or Col 1, Col 2... without one
			table.AddColumn("[grey]#[/]");
			for (int c = 0; c < columns; c++)
			{
				string name = hasHeader ? firstRow[c] : "Col " + (c + 1);
				table.AddColumn("[yellow]" + Markup.Escape(name) + "[/]");
			}

			// tail numbering starts part way into the file
			long number = mode == "tail" ? total - rows.Count + 1 : 1;

			foreach (var row in rows)
			{
				var cells = new List<string> { "[grey]" + number + "[/]" };
				for (int c = 0; c < columns; c++)
				{
					// pad short rows, cut long ones
					string value = c < row.Length ? row[c] : "";
					cells.Add("[cyan]" + Markup.Escape(value) + "[/]");
				}
				table.AddRow(cells.ToArray());
				number++;
			}

			string shown = mode == "all" ? "" : $"Showing {(mode == "head" ? "first" : "last")} {rows.Count} of {total} rows. ";
			string header = hasHeader ? "Header row detected." : "No header row found, columns are numbered.";
			summary = shown + header;

			return table;
		}

		public void DisplayCSV(string filePath, string mode = "all", int count = 10)
		{
			if (!System.IO.File.Exists(filePath))
			{
				Console.WriteLine("File not found.");
				return;
			}

			AnsiConsole.Write(ParseCSV(filePath, mode, count));
			AnsiConsole.MarkupLine("[grey]" + Markup.Escape(summary) + "[/]");
		}

		// looks like a header if it's all text: no blanks, numbers, dates or repeats
		private static bool LooksLikeHeader(string[] row)
		{
			foreach (var cell in row)
			{
				if (cell.Trim() == "") return false;
				if (double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return false;
				if (DateTime.TryParse(cell, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
			}

			return row.Select(c => c.Trim().ToLower()).Distinct().Count() == row.Length;
		}
	


		// show code <file> [head|tail] [count]
		public void DisplayCode(string file, string mode = "all", int count = 20, bool lineNumbers = false)
		{
			if (!System.IO.File.Exists(file))
			{
				Console.WriteLine("File not found.");
				return;
			}

			PrintHighlighted(System.IO.File.ReadAllLines(file), Path.GetExtension(file), mode, count, lineNumbers);
		}

		// the extension picks the grammar (.cs, .json, ...)
		private void PrintHighlighted(string[] lines, string extension, string mode, int count, bool lineNumbers)
		{
			// theme + grammars
			var options = new RegistryOptions(ThemeName.DarkPlus);
			var registry = new TextMateSharp.Registry.Registry(options);
			var theme = registry.GetTheme();

			string? scope = options.GetScopeByExtension(extension);
			if (scope == null)
			{
				Console.WriteLine("No syntax highlighting available for " + extension);
				return;
			}
			var grammar = registry.LoadGrammar(scope);

			int first = 0;
			int last = lines.Length;
			if (mode == "head") last = Math.Min(count, lines.Length);
			if (mode == "tail") first = Math.Max(0, lines.Length - count);

			// tokenize every line up to the end, even for tail, so multi-line comments and strings still get the right colour
			IStateStack? state = null;
			for (int n = 0; n < last; n++)
			{
				string line = lines[n];
				var result = grammar.TokenizeLine(line, state, TimeSpan.MaxValue);
				state = result.RuleStack;

				if (n < first) continue; // only needed to keep the state right

				var markup = new StringBuilder();
				if (lineNumbers)
				{
					markup.Append("[grey]" + (n + 1).ToString().PadLeft(4) + "[/]  ");
				}

				foreach (var token in result.Tokens)
				{
					int start = Math.Min(token.StartIndex, line.Length);
					int end = Math.Min(token.EndIndex, line.Length);
					string text = line.Substring(start, end - start);

					int foreground = -1;
					foreach (var rule in theme.Match(token.Scopes))
					{
						if (foreground == -1 && rule.foreground > 0) foreground = rule.foreground;
					}

					if (foreground == -1)
					{
						markup.Append(Markup.Escape(text));
					}
					else
					{
						markup.Append("[" + theme.GetColor(foreground) + "]" + Markup.Escape(text) + "[/]");
					}
				}

				AnsiConsole.MarkupLine(markup.ToString());
			}

			if (mode != "all")
			{
				AnsiConsole.MarkupLine("[grey]Showing " + (mode == "head" ? "first " : "last ") + (last - first) + " of " + lines.Length + " lines.[/]");
			}
		}

		// show json <file or text> [head|tail] [count]
		public void DisplayJson(string input, string mode = "all", int count = 20, bool lineNumbers = false)
		{
			// input is a file, or the json itself (piped or quoted)
			string text = System.IO.File.Exists(input) ? System.IO.File.ReadAllText(input) : input;
			string shown = text;

			if (text.Trim() == "")
			{
				AnsiConsole.MarkupLine("[yellow]Nothing to show.[/] Give a file, some JSON text, or pipe text in.");
				return;
			}

			try
			{
				var parseOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
				using var doc = JsonDocument.Parse(text, parseOptions);

				using var stream = new MemoryStream();
				using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
				{
					doc.RootElement.WriteTo(writer);
				}
				shown = Encoding.UTF8.GetString(stream.ToArray());

				bool alreadyFormatted = text.Replace("\r\n", "\n").Trim() == shown.Replace("\r\n", "\n").Trim();
				AnsiConsole.MarkupLine(alreadyFormatted
					? "[green]Valid JSON, already formatted.[/]"
					: "[green]Valid JSON.[/] [yellow]Reformatted to make it easier to read.[/]");
			}
			catch (JsonException ex)
			{
				// say what is wrong, but still show the text
				// strip the line info the message ends with, we print our own
				AnsiConsole.MarkupLine("[red]Not valid JSON:[/] " + Markup.Escape(ex.Message.Split(" Path:")[0].Split(" LineNumber:")[0]));
				AnsiConsole.MarkupLine($"[red]Problem is near line {(ex.LineNumber ?? 0) + 1}, column {(ex.BytePositionInLine ?? 0) + 1}.[/] [grey]Showing the text as it is.[/]");
			}

			PrintHighlighted(shown.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'), ".json", mode, count, lineNumbers);
		}

		// show xml <file or text> [head|tail] [count]
		// xaml is the same but with every attribute on its own line
		public void DisplayXml(string input, string mode = "all", int count = 20, bool xaml = false, bool lineNumbers = false)
		{
			// input is a file, or the xml itself (piped or quoted)
			string text = System.IO.File.Exists(input) ? System.IO.File.ReadAllText(input) : input;
			string shown = text;

			if (text.Trim() == "")
			{
				AnsiConsole.MarkupLine("[yellow]Nothing to show.[/] Give a file, some XML text, or pipe text in.");
				return;
			}

			try
			{
				// ignore DTDs so a file can't pull in other files
				using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
				var doc = XDocument.Load(reader);

				var settings = new XmlWriterSettings
				{
					Indent = true,
					IndentChars = xaml ? "    " : "  ",
					NewLineOnAttributes = xaml,
					OmitXmlDeclaration = true // added back below so it keeps its own encoding text
				};

				using var stringWriter = new StringWriter();
				using (var writer = XmlWriter.Create(stringWriter, settings))
				{
					doc.Save(writer);
				}
				shown = stringWriter.ToString();

				if (xaml)
				{
					// one attribute doesn't need its own line
					shown = Regex.Replace(shown, @"<([\w:.]+)\r?\n\s+([\w:.]+=""[^""]*"")\s*(/?)>",
						m => "<" + m.Groups[1].Value + " " + m.Groups[2].Value + (m.Groups[3].Value == "/" ? " />" : ">"));
				}

				if (doc.Declaration != null)
				{
					shown = doc.Declaration + "\n" + shown;
				}

				bool alreadyFormatted = text.Replace("\r\n", "\n").Trim() == shown.Replace("\r\n", "\n").Trim();
				AnsiConsole.MarkupLine(alreadyFormatted
					? "[green]Valid XML, already formatted.[/]"
					: "[green]Valid XML.[/] [yellow]Reformatted to make it easier to read.[/]");
			}
			catch (XmlException ex)
			{
				// say what is wrong, but still show the text
				AnsiConsole.MarkupLine("[red]Not valid XML:[/] " + Markup.Escape(ex.Message));
				AnsiConsole.MarkupLine($"[red]Problem is near line {ex.LineNumber}, column {ex.LinePosition}.[/] [grey]Showing the text as it is.[/]");
			}

			PrintHighlighted(shown.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'), ".xml", mode, count, lineNumbers);
		}

		// show containers [filter]
		public async Task DisplayContainers(string filter = "")
		{
			IList<ContainerListResponse> containers;

			try
			{
				// named pipe on windows, socket file elsewhere. DOCKER_HOST wins if set
				string dockerAddress = Environment.GetEnvironmentVariable("DOCKER_HOST")
					?? (OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock");
				using var client = new DockerClientConfiguration(new Uri(dockerAddress)).CreateClient();
				containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
			}
			catch (Exception ex)
			{
				AnsiConsole.MarkupLine("[red]Could not talk to Docker.[/] Is Docker Desktop running?");
				AnsiConsole.MarkupLine("[grey]" + Markup.Escape(ex.Message) + "[/]");
				return;
			}

			if (filter != "")
			{
				containers = containers.Where(c => MatchesFilter(c, filter)).ToList();
			}

			if (containers.Count == 0)
			{
				AnsiConsole.MarkupLine(filter == "" ? "[yellow]No containers found.[/]" : "[yellow]No containers match[/] '" + Markup.Escape(filter) + "'.");
				return;
			}

			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
			table.AddColumn("[yellow]Name[/]");
			table.AddColumn("[yellow]Image[/]");
			table.AddColumn("[yellow]State[/]");
			table.AddColumn("[yellow]Status[/]");
			table.AddColumn("[yellow]Ports[/]");
			table.AddColumn("[yellow]Mounts[/]");
			table.AddColumn("[yellow]ID[/]");

			foreach (var c in containers.OrderBy(c => c.Names.FirstOrDefault()))
			{
				string name = string.Join(", ", c.Names.Select(n => n.TrimStart('/')));

				// green running, red stopped, yellow in between
				string colour = c.State switch
				{
					"running" => "green",
					"exited" or "dead" => "red",
					"paused" or "restarting" => "yellow",
					_ => "grey"
				};

				// 5432->5432/tcp is published to the host, 5432/tcp is only inside docker
				var ports = c.Ports
					.Select(p => p.PublicPort != 0 ? $"{p.PublicPort}->{p.PrivatePort}/{p.Type}" : $"{p.PrivatePort}/{p.Type}")
					.Distinct()
					.ToList();

				table.AddRow(
					$"[cyan]{Markup.Escape(name)}[/]",
					Markup.Escape(c.Image),
					$"[{colour}]{Markup.Escape(c.State)}[/]",
					$"[{colour}]{Markup.Escape(c.Status)}[/]",
					ports.Count == 0 ? "[grey]-[/]" : Markup.Escape(string.Join("\n", ports)),
					$"[grey]{c.Mounts?.Count ?? 0}[/]",
					$"[grey]{c.ID.Substring(0, 12)}[/]");
			}

			AnsiConsole.Write(table);

			int running = containers.Count(c => c.State == "running");
			AnsiConsole.MarkupLine($"[grey]{containers.Count} container{(containers.Count == 1 ? "" : "s")} ({running} running).[/]");
		}

		// * and ? wildcards, no wildcard means contains
		private static bool MatchesFilter(ContainerListResponse c, string filter)
		{
			if (!filter.Contains('*') && !filter.Contains('?'))
			{
				filter = "*" + filter + "*";
			}

			string pattern = "^" + Regex.Escape(filter).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
			var regex = new Regex(pattern, RegexOptions.IgnoreCase);

			// name, image or id
			string name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "";
			return regex.IsMatch(name) || regex.IsMatch(c.Image) || regex.IsMatch(c.ID);
		}


		// show locate file <name> [ext] [path] [filter]
		public void DisplayLocate(string fileName, string extension = "", string path = "", string filter = "", bool useRegex = false)
		{
			string root = Path.GetFullPath(path == "" ? System.IO.Directory.GetCurrentDirectory() : path);

			if (!System.IO.Directory.Exists(root))
			{
				AnsiConsole.MarkupLine("[red]Folder not found:[/] " + Markup.Escape(root));
				return;
			}

			AnsiConsole.MarkupLine("[grey]Searching " + Markup.Escape(root) + " ...[/]");

			List<string> files;
			try
			{
				files = fileLocation(root, fileName, extension, filter, useRegex);
			}
			catch (ArgumentException ex) // a regular expression that isn't valid
			{
				AnsiConsole.MarkupLine("[red]That is not a valid regular expression:[/] " + Markup.Escape(ex.Message));
				return;
			}

			if (files.Count == 0)
			{
				AnsiConsole.MarkupLine("[yellow]No files found.[/]");
				return;
			}

			int maxRows = 100;

			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
			table.AddColumn("[yellow]Name[/]");
			table.AddColumn("[yellow]Size[/]");
			table.AddColumn("[yellow]Modified[/]");
			table.AddColumn("[yellow]Folder (full path)[/]");

			foreach (var file in files.Take(maxRows))
			{
				var info = new FileInfo(file);
				string folder = info.DirectoryName ?? root;

				table.AddRow(
					$"[cyan]{Markup.Escape(info.Name)}[/]",
					Formatter.FormatSize(info.Length),
					info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
					$"[grey]{Markup.Escape(folder)}[/]");
			}

			AnsiConsole.Write(table);

			AnsiConsole.MarkupLine(files.Count > maxRows
				? $"[grey]Showing the first {maxRows} of {files.Count} files. Add a filter or a more exact name to narrow it down.[/]"
				: $"[grey]{files.Count} file{(files.Count == 1 ? "" : "s")} found.[/]");
		}

		// show locate text <text> [path] [ext]
		public void DisplayLocateText(string text, string path = "", string extension = "", bool useRegex = false)
		{
			string root = Path.GetFullPath(path == "" ? System.IO.Directory.GetCurrentDirectory() : path);

			if (!System.IO.Directory.Exists(root))
			{
				AnsiConsole.MarkupLine("[red]Folder not found:[/] " + Markup.Escape(root));
				return;
			}

			AnsiConsole.MarkupLine("[grey]Searching inside files in " + Markup.Escape(root) + " for '" + Markup.Escape(text) + "' ...[/]");

			int maxMatches = 1000;
			Regex matcher;
			List<(string File, int Line, string Text)> matches;

			try
			{
				matcher = MakeMatcher(text, useRegex, false);
				matches = textLocation(text, root, extension, maxMatches, useRegex);
			}
			catch (ArgumentException ex) // a regular expression that isn't valid
			{
				AnsiConsole.MarkupLine("[red]That is not a valid regular expression:[/] " + Markup.Escape(ex.Message));
				return;
			}

			if (matches.Count == 0)
			{
				AnsiConsole.MarkupLine("[yellow]No matches found.[/]");
				return;
			}

			int maxRows = 100;

			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
			table.AddColumn("[yellow]File (full path)[/]");
			table.AddColumn("[yellow]Line[/]");
			table.AddColumn("[yellow]Text[/]");

			foreach (var m in matches.Take(maxRows))
			{
				table.AddRow(
					$"[cyan]{Markup.Escape(m.File)}[/]",
					$"[grey]{m.Line}[/]",
					HighlightMatch(m.Text, matcher));
			}

			AnsiConsole.Write(table);

			int files = matches.Select(m => m.File).Distinct().Count();
			string total = matches.Count >= maxMatches ? maxMatches + "+" : matches.Count.ToString();
			AnsiConsole.MarkupLine($"[grey]{total} match{(matches.Count == 1 ? "" : "es")} in {files} file{(files == 1 ? "" : "s")}.[/]");

			if (matches.Count > maxRows)
			{
				AnsiConsole.MarkupLine($"[grey]Showing the first {maxRows}. Add an extension or a more exact text to narrow it down.[/]");
			}
		}

		// show locate directory [path] [pattern]
		public void DisplayLocateDirectory(string path = "", string pattern = "", bool useRegex = false)
		{
			string root = Path.GetFullPath(path == "" ? System.IO.Directory.GetCurrentDirectory() : path);

			if (!System.IO.Directory.Exists(root))
			{
				AnsiConsole.MarkupLine("[red]Folder not found:[/] " + Markup.Escape(root));
				return;
			}

			AnsiConsole.MarkupLine("[grey]Listing folders in " + Markup.Escape(root) + " ...[/]");

			List<string> folders;
			try
			{
				folders = directoryLocation(root, pattern, useRegex);
			}
			catch (ArgumentException ex) // a regular expression that isn't valid
			{
				AnsiConsole.MarkupLine("[red]That is not a valid regular expression:[/] " + Markup.Escape(ex.Message));
				return;
			}

			if (folders.Count == 0)
			{
				AnsiConsole.MarkupLine(pattern == ""
					? "[yellow]No folders found[/] below " + Markup.Escape(root) + "."
					: "[yellow]No folders matching '" + Markup.Escape(pattern) + "' found[/] below " + Markup.Escape(root) + ".");
				AnsiConsole.MarkupLine("[grey]Only folders inside the start folder are listed. To start in a folder, give a path such as ." + Path.DirectorySeparatorChar + "name.[/]");
				return;
			}

			int maxRows = 100;

			var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
			table.AddColumn("[yellow]Folder (full path)[/]");
			table.AddColumn("[yellow]Files[/]");
			table.AddColumn("[yellow]Subfolders[/]");
			table.AddColumn("[yellow]Modified[/]");

			foreach (var folder in folders.Take(maxRows))
			{
				var info = new DirectoryInfo(folder);

				// direct children only, - if we can't look
				string fileCount = "-";
				string folderCount = "-";
				try
				{
					fileCount = info.GetFiles().Length.ToString();
					folderCount = info.GetDirectories().Length.ToString();
				}
				catch (Exception)
				{
				}

				table.AddRow(
					$"[cyan]{Markup.Escape(folder)}[/]",
					fileCount,
					folderCount,
					info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
			}

			AnsiConsole.Write(table);

			AnsiConsole.MarkupLine(folders.Count > maxRows
				? $"[grey]Showing the first {maxRows} of {folders.Count} folders. Add a pattern to narrow it down.[/]"
				: $"[grey]{folders.Count} folder{(folders.Count == 1 ? "" : "s")} found.[/]");
		}

		// shorten around the match and highlight every hit
		private static string HighlightMatch(string line, Regex matcher)
		{
			line = line.Replace("\t", " ").Trim();

			// long line: keep a window around the first hit
			var hits = Hits(matcher, line);
			string before = "";
			string after = "";
			if (line.Length > 100 && hits.Count > 0)
			{
				int start = Math.Max(0, hits[0].Index - 30);
				int length = Math.Min(100, line.Length - start);
				before = start > 0 ? "..." : "";
				after = start + length < line.Length ? "..." : "";
				line = line.Substring(start, length);
				hits = Hits(matcher, line);
			}

			var markup = new StringBuilder(Markup.Escape(before));
			int position = 0;

			foreach (var hit in hits)
			{
				if (hit.Index < position) continue;

				markup.Append(Markup.Escape(line.Substring(position, hit.Index - position)));
				markup.Append("[black on yellow]" + Markup.Escape(hit.Value) + "[/]");
				position = hit.Index + hit.Length;
			}

			markup.Append(Markup.Escape(line.Substring(position)));
			markup.Append(Markup.Escape(after));
			return markup.ToString();
		}

		// runs a program, returns "" if it is missing or fails
		internal static string RunProgram(string program, string arguments = "")
		{
			try
			{
				var info = new ProcessStartInfo(program, arguments)
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = Process.Start(info);
				if (process == null) return "";

				process.BeginErrorReadLine(); // error text is thrown away, but it has to be read or the program can get stuck
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				return output;
			}
			catch (Exception)
			{
				return "";
			}
		}


		// builds the matcher: * and ? wildcards, or a real regex with -r. Ignores case.
		// wholeName: names have to fit completely when there is a wildcard, text can match anywhere in a line.
		// A bad regex throws an ArgumentException.
		internal static Regex MakeMatcher(string pattern, bool useRegex, bool wholeName)
		{
			string expression = pattern;

			if (!useRegex)
			{
				bool hasWildcard = pattern.Contains('*') || pattern.Contains('?');
				expression = Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".");

				// wildcard = whole name must match, plain text = contains
				if (wholeName && hasWildcard)
				{
					expression = "^" + expression + "$";
				}
			}

			// slow patterns time out after a second, see IsMatch
			return new Regex(expression, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
		}

		internal static bool IsMatch(Regex matcher, string text)
		{
			try
			{
				return matcher.IsMatch(text);
			}
			catch (RegexMatchTimeoutException)
			{
				return false;
			}
		}

		private static List<Match> Hits(Regex matcher, string text)
		{
			try
			{
				return matcher.Matches(text).Where(m => m.Length > 0).ToList();
			}
			catch (RegexMatchTimeoutException)
			{
				return new List<Match>();
			}
		}

		// recursive file search. path "" = current folder, extension with or without the dot,
		// filter = text the full path has to contain. No wildcard in the name means contains.
		// folders we can't open are skipped
		public static List<string> fileLocation(string path = "", string fileName = "*", string extension = "", string filter = "", bool useRegex = false)
		{
			if (path == "")
			{
				path = System.IO.Directory.GetCurrentDirectory();
			}

			if (!System.IO.Directory.Exists(path))
			{
				return new List<string>();
			}

			if (fileName == "")
			{
				fileName = "*";
			}

			if (extension != "" && !extension.StartsWith("."))
			{
				extension = "." + extension;
			}

			var matcher = MakeMatcher(fileName, useRegex, true);

			var files = System.IO.Directory.EnumerateFiles(path, "*", SearchEverything())
				.Where(f => extension == "" || string.Equals(Path.GetExtension(f), extension, StringComparison.OrdinalIgnoreCase))
				.Where(f => IsMatch(matcher, Path.GetFileName(f)));

			if (filter != "")
			{
				files = files.Where(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase));
			}

			return files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
		}

		// recursive text search, stops at maxMatches lines. Skips binaries, files over 20 MB, .git and node_modules
		public static List<(string File, int Line, string Text)> textLocation(string text, string path = "", string extension = "", int maxMatches = 1000, bool useRegex = false)
		{
			var matches = new List<(string File, int Line, string Text)>();

			if (text == "")
			{
				return matches;
			}

			var matcher = MakeMatcher(text, useRegex, false);

			foreach (var file in fileLocation(path, "*", extension))
			{
				char sep = Path.DirectorySeparatorChar;
				if (file.Contains(sep + ".git" + sep) || file.Contains(sep + "node_modules" + sep)) continue;

				try
				{
					var info = new FileInfo(file);
					if (info.Length == 0 || info.Length > 20 * 1024 * 1024) continue;

					// a zero byte early on means binary
					var start = new byte[4096];
					int read;
					using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						read = stream.Read(start, 0, start.Length);
					}
					if (Array.IndexOf(start, (byte)0, 0, read) >= 0) continue;

					int number = 0;
					foreach (var line in System.IO.File.ReadLines(file))
					{
						number++;

						if (IsMatch(matcher, line))
						{
							matches.Add((file, number, line));
							if (matches.Count >= maxMatches) return matches;
						}
					}
				}
				catch (Exception)
				{
					// locked or unreadable, skip it
				}
			}

			return matches;
		}

		// recursive folder search, optionally by name
		public static List<string> directoryLocation(string path = "", string pattern = "", bool useRegex = false)
		{
			if (path == "")
			{
				path = System.IO.Directory.GetCurrentDirectory();
			}

			if (!System.IO.Directory.Exists(path))
			{
				return new List<string>();
			}

			var matcher = pattern == "" ? null : MakeMatcher(pattern, useRegex, true);

			return System.IO.Directory.EnumerateDirectories(path, "*", SearchEverything())
				.Where(d => matcher == null || IsMatch(matcher, Path.GetFileName(d)))
				.OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		// skip what we can't open, don't follow junctions (they can loop)
		private static EnumerationOptions SearchEverything()
		{
			return new EnumerationOptions
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.ReparsePoint
			};
		}
	}
}
