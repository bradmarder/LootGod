using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

var inputFolder = "";
var outputFolder = "";

if (!Directory.Exists(inputFolder))
{
	Console.Error.WriteLine($"Input folder does not exist: {inputFolder}");
	return;
}

var regex = new Regex(@"dragitem(\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

var files = Directory.EnumerateFiles(inputFolder, "*.png", SearchOption.TopDirectoryOnly)
	.Where(f => regex.IsMatch(Path.GetFileName(f)))
	.OrderBy(x => int.Parse(x.Split("dragitem").Last().Split('.')[0]))
	.ToList();

if (files.Count == 0)
{
	Console.WriteLine("No files matching pattern 'dragitemN.png' were found in the input folder.");
	return;
}

Console.WriteLine($"Found {files.Count} files. Output folder: {outputFolder}");

var n = 500;
foreach (var file in files)
{
	var fileName = Path.GetFileName(file);
	try
	{
		using var image = await Image.LoadAsync(file);

		if (image.Width != 256 || image.Height != 256)
		{
			throw new Exception("Failed assumption - images must be 256x256");
		}

		const int gridSize = 240; // we only care about the top left 240x240 region

		int cellW = gridSize / 6;
		int cellH = gridSize / 6;

		for (int col = 0; col < 6; col++)
		{
			for (int row = 0; row < 6; row++)
			{
				int x = col * cellW;
				int y = row * cellH;

				var rect = new Rectangle(x, y, cellW, cellH);

				using var icon = image.Clone(x => x.Crop(rect));

				var outName = $"item_{n}.png";
				var outPath = Path.Combine(outputFolder, outName);

				await icon.SaveAsPngAsync(outPath);
				n++;
			}
		}

		Console.WriteLine($"Processed {fileName}");
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"Failed to process {fileName}: {ex.Message}");
	}
}

Console.WriteLine("Done.");
