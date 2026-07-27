using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	public void InsertRow(int index)
	{
		if (index < 0 || index > Model.RowCount)
		{
			return;
		}

		PushUndoSnapshot();
		Model.RowHeaders.Insert(index, (index + 1).ToString());

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out var r, out var c);
			if (r >= index)
			{
				newCells[$"{r + 1},{c}"] = kvp.Value;
			}
			else
			{
				newCells[kvp.Key] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(RowHeights, index, insert: true, DefaultRowHeight);

		AdjustSelectionAfterRowInsert(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void DeleteRow(int index)
	{
		if (index < 0 || index >= Model.RowCount || Model.RowCount <= 1)
		{
			return;
		}

		PushUndoSnapshot();
		Model.RowHeaders.RemoveAt(index);

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out var r, out var c);
			if (r < index)
			{
				newCells[kvp.Key] = kvp.Value;
			}
			else if (r > index)
			{
				newCells[$"{r - 1},{c}"] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(RowHeights, index, insert: false, DefaultRowHeight);

		AdjustSelectionAfterRowDelete(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void InsertColumn(int index)
	{
		if (index < 0 || index > Model.ColumnCount)
		{
			return;
		}

		PushUndoSnapshot();
		// Default label for the new column only; preserve custom headers on existing columns.
		Model.ColumnHeaders.Insert(index, GetColumnLetter(index));

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out var r, out var c);
			if (c >= index)
			{
				newCells[$"{r},{c + 1}"] = kvp.Value;
			}
			else
			{
				newCells[kvp.Key] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(ColumnWidths, index, insert: true, DefaultColumnWidth);
		ShiftFiltersAfterColumnInsert(index);

		AdjustSelectionAfterColumnInsert(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void DeleteColumn(int index)
	{
		if (index < 0 || index >= Model.ColumnCount || Model.ColumnCount <= 1)
		{
			return;
		}

		PushUndoSnapshot();
		Model.ColumnHeaders.RemoveAt(index);

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out var r, out var c);
			if (c < index)
			{
				newCells[kvp.Key] = kvp.Value;
			}
			else if (c > index)
			{
				newCells[$"{r},{c - 1}"] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(ColumnWidths, index, insert: false, DefaultColumnWidth);
		ShiftFiltersAfterColumnDelete(index);

		AdjustSelectionAfterColumnDelete(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}
}
