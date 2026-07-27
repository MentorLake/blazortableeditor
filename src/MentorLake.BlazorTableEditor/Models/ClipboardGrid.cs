namespace MentorLake.BlazorTableEditor.Models;

public sealed class ClipboardGrid
{
	public int Rows { get; init; }
	public int Cols { get; init; }
	public CellValue[,] Cells { get; init; } = new CellValue[0, 0];

	public static ClipboardGrid Empty { get; } = new() { Rows = 0, Cols = 0, Cells = new CellValue[0, 0] };

	public bool IsEmpty => Rows == 0 || Cols == 0;

	public string ToTsv()
	{
		if (IsEmpty)
		{
			return string.Empty;
		}

		var lines = new string[Rows];
		for (var r = 0; r < Rows; r++)
		{
			var parts = new string[Cols];
			for (var c = 0; c < Cols; c++)
			{
				parts[c] = EscapeTsv(Cells[r, c]?.ToString() ?? string.Empty);
			}

			lines[r] = string.Join('\t', parts);
		}

		return string.Join('\n', lines);
	}

	public static ClipboardGrid FromTsv(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return Empty;
		}

		text = text.Replace("\r\n", "\n").Replace('\r', '\n');
		if (text.EndsWith('\n'))
		{
			text = text[..^1];
		}

		var lines = text.Split('\n');
		if (lines.Length == 0)
		{
			return Empty;
		}

		var rows = lines.Length;
		var cols = 1;
		var parsed = new string[rows][];
		for (var r = 0; r < rows; r++)
		{
			parsed[r] = SplitTsvLine(lines[r]);
			cols = Math.Max(cols, parsed[r].Length);
		}

		var cells = new CellValue[rows, cols];
		for (var r = 0; r < rows; r++)
		{
			for (var c = 0; c < cols; c++)
			{
				if (c < parsed[r].Length && !string.IsNullOrEmpty(parsed[r][c]))
				{
					cells[r, c] = new CellValue(parsed[r][c]);
				}
			}
		}

		return new ClipboardGrid { Rows = rows, Cols = cols, Cells = cells };
	}

	private static string EscapeTsv(string value)
	{
		if (value.Contains('\t') || value.Contains('\n') || value.Contains('\r'))
		{
			return value.Replace('\t', ' ').Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
		}

		return value;
	}

	private static string[] SplitTsvLine(string line) => line.Split('\t');
}
