# show

A tool that displays various types of information.

A small command line tool I use to look at things on a machine without remembering ten different commands: files, folders, drives, CSV/JSON/XML, running services, open ports, docker containers and so on. Output is formatted with [Spectre.Console](https://spectreconsole.net/).

Works on Windows, Linux and macOS. Windows gets the most detail (hardware info, window titles for `apps`), the other two fall back to what the system exposes.

## Commands

```
show file <path>                        details of a file
show dir <path>                         folder, subfolders and sizes
show csv <file> [head|tail] [count]     csv as a table, guesses the header
show code <file> [head|tail] [count]    syntax highlighted source (-n for line numbers)
show json|xml|xaml <file or text>       validate, reformat and highlight (also takes piped text)
show locate file|text|directory ...     find files, text inside files or folders (-r for regex)
show drives                             drives and how full they are
show pc [os|cpu|gpu|ram|board|disk]     hardware and system info
show ip                                 local address, dns check, network interfaces
show ports [port or name]               open ports and who is using them
show services [filter]                  running services with ram and cpu use
show apps [filter]                      running apps with ram and cpu use
show containers [filter]                docker containers, wildcards allowed
show help [command]                     the full manual
```

`show help <command>` has examples for each one.

## Install

Grab the file for your system from the [releases](https://github.com/blivodev/show/releases) page, they are single files and don't need .NET installed.

- Windows: `show-win-x64.exe`, put it somewhere on your PATH.
- Linux / macOS: extract the `.tar.gz`, then `chmod +x show` if needed and move it onto your PATH.
- macOS also needs `xattr -d com.apple.quarantine show` the first time, the binaries aren't signed.

## Build

Needs the .NET 10 SDK.

```
dotnet build
dotnet run -- help
```

A release build for one platform:

```
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

The colour list the charts use is `Colors.txt` (built into the exe). Put a `Colors.txt` next to the exe to use your own.

## License

MIT, see [LICENSE](LICENSE).
