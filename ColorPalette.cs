using Spectre.Console;

namespace show;

public static class ColorPalette
{
	// Colors.txt: one colour per line as Name,R,G,B. Edit it to taste.
	public static List<Color> Pick(int count)
	{
		// 16 colour terminals round custom colours to dark ones, so use the bright built-ins there
		if (AnsiConsole.Profile.Capabilities.ColorSystem < ColorSystem.EightBit)
		{
			Color[] basic = { Color.Red, Color.Yellow, Color.Lime, Color.Aqua, Color.Blue, Color.Fuchsia };
			int offset = Random.Shared.Next(basic.Length);
			int step = Math.Max(1, basic.Length / Math.Max(count, 1));
			var simple = new List<Color>();

			for (int i = 0; i < count; i++)
			{
				simple.Add(basic[(offset + i * step) % basic.Length]);
			}
			return simple;
		}

		var all = new List<Color>();

		foreach (var line in ColorLines())
		{
			var p = line.Split(',');
			if (p.Length != 4) continue;

			byte r = byte.Parse(p[1]);
			byte g = byte.Parse(p[2]);
			byte b = byte.Parse(p[3]);

			// too dark to see on a dark terminal
			if (Math.Max(r, Math.Max(g, b)) < 180) continue;

			// grey is already used for the folder's own files
			if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 60) continue;

			all.Add(new Color(r, g, b));
		}

		if (all.Count == 0)
		{
			all.AddRange(new[] { Color.Green, Color.Yellow, Color.Aqua, Color.Red, Color.Fuchsia, Color.Blue });
		}

		// spread the hues around the colour wheel, random start so it's not the same every run
		double start = Random.Shared.NextDouble() * 360;
		var used = new HashSet<int>();
		var result = new List<Color>();

		for (int i = 0; i < count; i++)
		{
			double target = (start + i * 360.0 / count) % 360;
			int best = -1;
			double bestDistance = double.MaxValue;

			for (int j = 0; j < all.Count; j++)
			{
				if (used.Contains(j) && used.Count < all.Count) continue;

				double hue = System.Drawing.Color.FromArgb(all[j].R, all[j].G, all[j].B).GetHue();
				double distance = Math.Abs(hue - target);
				distance = Math.Min(distance, 360 - distance);

				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = j;
				}
			}

			used.Add(best);
			result.Add(all[best]);
		}

		return result;
	}

	// the list is built into the exe, a Colors.txt next to it wins if you want your own
	private static string[] ColorLines()
	{
		string file = Path.Combine(AppContext.BaseDirectory, "Colors.txt");
		if (System.IO.File.Exists(file))
		{
			return System.IO.File.ReadAllLines(file);
		}

		using var stream = typeof(ColorPalette).Assembly.GetManifestResourceStream("show.Colors.txt");
		if (stream == null)
		{
			return Array.Empty<string>();
		}

		using var reader = new StreamReader(stream);
		return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}
}
