using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	public CellValue? GetValue(int row, int col) => Model.GetCell(row, col);

	public string GetCellDisplay(int row, int col) => GetValue(row, col)?.ToString() ?? string.Empty;

	public void SetValue(int row, int col, object? value, string? format = null)
	{
		PushUndoSnapshot();
		if (value is null || (value is string s && string.IsNullOrEmpty(s)))
		{
			Model.ClearCell(row, col);
		}
		else
		{
			var cell = GetValue(row, col) ?? new CellValue();
			cell.Value = value;
			if (format is not null)
			{
				cell.Format = format;
			}

			Model.SetCell(row, col, cell);
		}

		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void SetColumnHeader(int col, string? header)
	{
		if (col < 0 || col >= Model.ColumnCount)
		{
			return;
		}

		var value = header?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(value))
		{
			value = GetColumnLetter(col);
		}

		if (Model.ColumnHeaders[col] == value)
		{
			return;
		}

		PushUndoSnapshot();
		Model.ColumnHeaders[col] = value;
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void ClearSelectionValues()
	{
		PushUndoSnapshot();
		var region = GetEffectiveSelection();
		for (int r = region.StartRow; r <= region.EndRow; r++)
		{
			for (int c = region.StartCol; c <= region.EndCol; c++)
			{
				Model.ClearCell(r, c);
			}
		}

		NotifyDataChanged();
		NotifyStateChanged();
	}

	private static void ParseKey(string key, out int row, out int col)
	{
		var parts = key.Split(',');
		row = int.Parse(parts[0]);
		col = int.Parse(parts[1]);
	}

	private static string GetColumnLetter(int colIndex)
	{
		string letter = string.Empty;
		int index = colIndex;
		while (index >= 0)
		{
			letter = (char)('A' + (index % 26)) + letter;
			index = (index / 26) - 1;
		}

		return letter;
	}
}
