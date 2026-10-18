using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using show;
using Spectre.Console;

namespace show
{
	public class File
	{
		public FileInfo Info(string filePath)
		{

			var fi = new FileInfo(filePath);


			if (System.IO.File.Exists(filePath))
			{

				try
				{
					var fileInfo = new System.IO.FileInfo(filePath);

					var table = Formatter.FormatFileInfo(fileInfo);
					AnsiConsole.Write(table);
				


				}
				catch (Exception ex)
				{

					Console.WriteLine($"Error retrieving file information: {ex.Message}");
					fi = null;

				}

				

			}
			else
			{
				Console.WriteLine("File not found.");
			}

			return fi; 
		}
	}


}