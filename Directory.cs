using Spectre.Console;
using System.Runtime.Serialization;

namespace show;

public class Directory
{
	public DirectoryInfo Info(string dirPath)
	{

		DirectoryInfo di = null;

		if (System.IO.Directory.Exists(dirPath))
		{

			try
			{
				di = new System.IO.DirectoryInfo(dirPath);

				long rootSize = GetDirectorySize(dirPath);
				var dirTable = Formatter.FormatDirectoryInfo(di, rootSize / 1024 / 1024);
				AnsiConsole.Write(dirTable);

				var subdirs = GetSubDirectories(di);
				if (subdirs != null)
				{
					long[] sizes = new long[subdirs.Length];
					for (int i = 0; i < subdirs.Length; i++)
					{
						sizes[i] = GetDirectorySize(subdirs[i].FullName);
					}

					AnsiConsole.Write(Formatter.FormatSubDirectories(subdirs, sizes));
					AnsiConsole.Write(Formatter.VisualizeDirectorySize(subdirs, sizes, rootSize));
				}



			}
			catch (Exception ex)
			{

				Console.WriteLine($"Error retrieving directory information: {ex.Message}");
				

			}


		}
		else
		{
			Console.WriteLine("Directory not found.");
		}

		return di;

	}

	public DirectoryInfo[] GetSubDirectories(DirectoryInfo dir)
	{
		if (dir == null)
		{
			throw new ArgumentNullException(nameof(dir));
		}
		else
		{
			DirectoryInfo[] sd = dir.GetDirectories();

			if (sd.Length == 0 || sd == null)
			{
				sd = null;
				Console.WriteLine("No subdirectories found.");
			}			
			
			return sd;
		}
	}





	public static long GetDirectorySize(string dirPath)
	{
		{

			long totalSize = 0;
			try
			{
				Parallel.ForEach(
					new DirectoryInfo(dirPath).EnumerateFiles("*", SearchOption.AllDirectories),
					file => Interlocked.Add(ref totalSize, file.Length)
				);
			}
			catch (UnauthorizedAccessException) { /* Handle exception */ }

			return totalSize;
		}
	}

}