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

	public void DeleteRow(int index) => DeleteRows(index, index);

	public void DeleteRows(int startRow, int endRow)
	{
		if (Model.RowCount <= 1)
		{
			return;
		}

		if (startRow > endRow)
		{
			(startRow, endRow) = (endRow, startRow);
		}

		startRow = Math.Clamp(startRow, 0, Model.RowCount - 1);
		endRow = Math.Clamp(endRow, 0, Model.RowCount - 1);

		var count = endRow - startRow + 1;
		if (count <= 0 || count >= Model.RowCount)
		{
			return;
		}

		PushUndoSnapshot();

		for (var i = endRow; i >= startRow; i--)
		{
			Model.RowHeaders.RemoveAt(i);
		}

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out var r, out var c);
			if (r < startRow)
			{
				newCells[kvp.Key] = kvp.Value;
			}
			else if (r > endRow)
			{
				newCells[$"{r - count},{c}"] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMapRange(RowHeights, startRow, endRow);

		AdjustSelectionAfterRowsDelete(startRow, endRow);
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

	public void DeleteColumn(int index) => DeleteColumns(index, index);

	public void DeleteColumns(int startCol, int endCol)
	{
		if (Model.ColumnCount <= 1)
		{
			return;
		}

		if (startCol > endCol)
		{
			(startCol, endCol) = (endCol, startCol);
		}

		startCol = Math.Clamp(startCol, 0, Model.ColumnCount - 1);
		endCol = Math.Clamp(endCol, 0, Model.ColumnCount - 1);

		var count = endCol - startCol + 1;
		if (count <= 0 || count >= Model.ColumnCount)
		{
			return;
		}

		PushUndoSnapshot();

		for (var i = endCol; i >= startCol; i--)
		{
			Model.ColumnHeaders.RemoveAt(i);
		}

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out var r, out var c);
			if (c < startCol)
			{
				newCells[kvp.Key] = kvp.Value;
			}
			else if (c > endCol)
			{
				newCells[$"{r},{c - count}"] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMapRange(ColumnWidths, startCol, endCol);
		ShiftFiltersAfterColumnsDelete(startCol, endCol);

		AdjustSelectionAfterColumnsDelete(startCol, endCol);
		NotifyDataChanged();
		NotifyStateChanged();
	}
}
