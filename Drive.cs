using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Spectre.Console;

namespace show
{
	public class Drive
	{

		public DriveInfo[] GetDrives()
		{

			return DriveInfo.GetDrives();

		}


		public void DisplayDrives()
		{

			var ready = new List<DriveInfo>();

			foreach (DriveInfo d in GetDrives())
			{
				// skip the pseudo mounts (/proc, tmpfs, ...) on linux and mac
				if (!OperatingSystem.IsWindows() && (d.DriveType == DriveType.Ram || d.DriveType == DriveType.Unknown || (d.IsReady && d.TotalSize == 0)))
				{
					continue;
				}

				if (d.IsReady)
				{
					ready.Add(d);
				}
				else if (OperatingSystem.IsWindows())
				{
					Console.WriteLine($"Drive {d.Name} is not ready.");
				}
			}

			// same colours for the table and the chart
			var colors = ColorPalette.Pick(ready.Count);

			AnsiConsole.Write(Formatter.FormatDriveInfo(ready.ToArray(), colors));
			AnsiConsole.Write(Formatter.VisualizeDriveSize(ready.ToArray(), colors));

		}
		}	}
