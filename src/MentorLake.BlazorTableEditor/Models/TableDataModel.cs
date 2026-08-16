using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MentorLake.BlazorTableEditor.Models;

public class TableDataModel
{
	public List<string> ColumnHeaders { get; set; } = new();
	public List<string> RowHeaders { get; set; } = new();
	public Dictionary<string, CellValue> Cells { get; set; } = new();

	[JsonIgnore] public int RowCount => RowHeaders.Count;

	[JsonIgnore] public int ColumnCount => ColumnHeaders.Count;

	public TableDataModel(int rows = 100, int cols = 26)
	{
		for (var c = 0; c < cols; c++)
		{
			ColumnHeaders.Add(GetColumnLetter(c));
		}

		for (var r = 0; r < rows; r++)
		{
			RowHeaders.Add((r + 1).ToString());
		}
	}

	private static string GetColumnLetter(int col)
	{
		var letter = "";
		var index = col;
		while (index >= 0)
		{
			letter = (char)('A' + (index % 26)) + letter;
			index = (index / 26) - 1;
		}

		return letter;
	}

	public CellValue GetCell(int row, int col)
	{
		var key = GetKey(row, col);
		return Cells.TryGetValue(key, out var value) ? value : null;
	}

	public void SetCell(int row, int col, CellValue value)
	{
		var key = GetKey(row, col);
		if (value == null || (value.Value == null && string.IsNullOrEmpty(value.Format)))
		{
			Cells.Remove(key);
		}
		else
		{
			Cells[key] = value;
		}
	}

	public void ClearCell(int row, int col)
	{
		Cells.Remove(GetKey(row, col));
	}

	private static string GetKey(int row, int col) => $"{row},{col}";

	public string ToJson()
	{
		var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
		return JsonSerializer.Serialize(this, options);
	}

	public static TableDataModel FromJson(string json)
	{
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var model = JsonSerializer.Deserialize<TableDataModel>(json, options) ?? new TableDataModel();
		if (model.ColumnHeaders.Count == 0) model = new TableDataModel(100, 26);
		return model;
	}

	public string ToCsv(bool includeHeaders = true)
	{
		var (maxRow, maxCol) = GetUsedBounds();
		var rows = Math.Max(maxRow + 1, 1);
		var cols = Math.Max(Math.Max(maxCol + 1, ColumnHeaders.Count), 1);

		var sb = new StringBuilder();
		if (includeHeaders)
		{
			for (var c = 0; c < cols; c++)
			{
				if (c > 0) sb.Append(',');
				var header = c < ColumnHeaders.Count ? ColumnHeaders[c] : GetColumnLetter(c);
				sb.Append(EscapeCsvField(header));
			}

			sb.AppendLine();
		}

		for (var r = 0; r < rows; r++)
		{
			for (var c = 0; c < cols; c++)
			{
				if (c > 0) sb.Append(',');
				var cell = GetCell(r, c);
				sb.Append(EscapeCsvField(cell?.ToString() ?? string.Empty));
			}

			if (r < rows - 1)
			{
				sb.AppendLine();
			}
		}

		return sb.ToString();
	}

	public static TableDataModel FromCsv(string csv, bool firstRowIsHeader = true)
	{
		if (string.IsNullOrWhiteSpace(csv))
		{
			return new TableDataModel(10, 5);
		}

		var rows = ParseCsv(csv);
		if (rows.Count == 0)
		{
			return new TableDataModel(10, 5);
		}

		var headerOffset = firstRowIsHeader ? 1 : 0;
		var dataRowCount = Math.Max(rows.Count - headerOffset, 1);
		var colCount = rows.Max(r => r.Count);
		colCount = Math.Max(colCount, 1);

		var model = new TableDataModel(dataRowCount, colCount);
		model.Cells.Clear();

		if (firstRowIsHeader)
		{
			var headers = rows[0];
			for (var c = 0; c < colCount; c++)
			{
				model.ColumnHeaders[c] = c < headers.Count && !string.IsNullOrWhiteSpace(headers[c])
					? headers[c]
					: GetColumnLetter(c);
			}
		}

		for (var r = 0; r < dataRowCount; r++)
		{
			var sourceRow = r + headerOffset;
			if (sourceRow >= rows.Count)
			{
				break;
			}

			var fields = rows[sourceRow];
			for (var c = 0; c < fields.Count && c < colCount; c++)
			{
				var text = fields[c];
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}

				model.SetCell(r, c, new CellValue(ParseCellObject(text)));
			}
		}

		return model;
	}

	public (int MaxRow, int MaxCol) GetUsedBounds()
	{
		var maxRow = -1;
		var maxCol = -1;

		foreach (var key in Cells.Keys)
		{
			var parts = key.Split(',');
			if (parts.Length != 2)
			{
				continue;
			}

			if (int.TryParse(parts[0], out var r) && int.TryParse(parts[1], out var c))
			{
				maxRow = Math.Max(maxRow, r);
				maxCol = Math.Max(maxCol, c);
			}
		}

		if (maxRow < 0)
		{
			maxRow = Math.Max(RowCount - 1, 0);
		}

		if (maxCol < 0)
		{
			maxCol = Math.Max(ColumnCount - 1, 0);
		}

		return (maxRow, maxCol);
	}

	private static object ParseCellObject(string text)
	{
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
		{
			return i;
		}

		if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
		{
			return d;
		}

		return text;
	}

	private static string EscapeCsvField(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
		if (!mustQuote)
		{
			return value;
		}

		return "\"" + value.Replace("\"", "\"\"") + "\"";
	}

	private static List<List<string>> ParseCsv(string csv)
	{
		var result = new List<List<string>>();
		var row = new List<string>();
		var field = new StringBuilder();
		var inQuotes = false;

		for (var i = 0; i < csv.Length; i++)
		{
			var ch = csv[i];

			if (inQuotes)
			{
				if (ch == '"')
				{
					if (i + 1 < csv.Length && csv[i + 1] == '"')
					{
						field.Append('"');
						i++;
					}
					else
					{
						inQuotes = false;
					}
				}
				else
				{
					field.Append(ch);
				}

				continue;
			}

			switch (ch)
			{
				case '"':
					inQuotes = true;
					break;
				case ',':
					row.Add(field.ToString());
					field.Clear();
					break;
				case '\r':
					break;
				case '\n':
					row.Add(field.ToString());
					field.Clear();
					result.Add(row);
					row = new List<string>();
					break;
				default:
					field.Append(ch);
					break;
			}
		}

		if (inQuotes || field.Length > 0 || row.Count > 0)
		{
			row.Add(field.ToString());
			result.Add(row);
		}

		while (result.Count > 0 && result[^1].All(string.IsNullOrEmpty))
		{
			result.RemoveAt(result.Count - 1);
		}

		return result;
	}

	public void AddSampleData()
	{
		if (ColumnCount >= 5)
		{
			ColumnHeaders[0] = "Name";
			ColumnHeaders[1] = "Category";
			ColumnHeaders[2] = "Qty";
			ColumnHeaders[3] = "Status";
			ColumnHeaders[4] = "Notes";
		}

		string[] names = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel"];
		string[] categories = ["FR", "FR", "VG", "DY", "FR", "VG", "DY", "Unknown"];
		object[] qtys = [12, 5, 20, 8, 15, 3, new CellValue(3.14159) { Format = "N2" }, 7];
		string[] statuses = ["Open", "Closed", "Open", "Pending", "Open", "Closed", "Pending", "Open"];
		string[] notes = ["Hello", "", "Blazor Table Editor", "Rush", "", "Backorder", "Formatted qty", ""];

		for (var i = 0; i < names.Length; i++)
		{
			SetCell(i, 0, new CellValue(names[i]));
			SetCell(i, 1, new CellValue(categories[i]));
			if (qtys[i] is CellValue cv)
			{
				SetCell(i, 2, cv);
			}
			else
			{
				SetCell(i, 2, new CellValue(qtys[i]));
			}

			SetCell(i, 3, new CellValue(statuses[i]));
			if (!string.IsNullOrEmpty(notes[i]))
			{
				var note = new CellValue(notes[i]);
				if (i == 2)
				{
					note.BackgroundColor = "#e6f3ff";
				}

				SetCell(i, 4, note);
			}
		}
	}
}
