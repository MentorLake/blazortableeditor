using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	public void SetActiveCell(int row, int col, bool extendSelection = false, bool notify = true)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		col = Math.Clamp(col, 0, Model.ColumnCount - 1);

		ActiveCell = new CellPosition(row, col);

		if (extendSelection)
		{
			CurrentSelection = new CellRegion(
				SelectionAnchor.Row,
				SelectionAnchor.Col,
				row,
				col).Normalize();
		}
		else
		{
			SelectionAnchor = ActiveCell;
			CurrentSelection = new CellRegion(row, col, row, col);
		}

		if (notify)
		{
			NotifyStateChanged();
		}
	}

	public void UpdateSelectionTo(int row, int col, bool notify = true)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		col = Math.Clamp(col, 0, Model.ColumnCount - 1);

		CurrentSelection = new CellRegion(
			SelectionAnchor.Row,
			SelectionAnchor.Col,
			row,
			col).Normalize();

		if (notify)
		{
			NotifyStateChanged();
		}
	}

	public void UpdateSelection(CellRegion region)
	{
		CurrentSelection = region.Normalize();
		NotifyStateChanged();
	}

	public void ClearSelection()
	{
		CurrentSelection = new CellRegion(ActiveCell.Row, ActiveCell.Col, ActiveCell.Row, ActiveCell.Col);
		NotifyStateChanged();
	}

	public void SelectColumn(int col, bool extendSelection = false)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		col = Math.Clamp(col, 0, Model.ColumnCount - 1);
		var lastRow = Model.RowCount - 1;

		if (extendSelection)
		{
			var startCol = Math.Min(SelectionAnchor.Col, col);
			var endCol = Math.Max(SelectionAnchor.Col, col);
			ActiveCell = new CellPosition(0, col);
			CurrentSelection = new CellRegion(0, startCol, lastRow, endCol);
		}
		else
		{
			ActiveCell = new CellPosition(0, col);
			SelectionAnchor = ActiveCell;
			CurrentSelection = new CellRegion(0, col, lastRow, col);
		}

		NotifyStateChanged();
	}

	public void SelectRow(int row, bool extendSelection = false)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		var lastCol = Model.ColumnCount - 1;

		if (extendSelection)
		{
			var startRow = Math.Min(SelectionAnchor.Row, row);
			var endRow = Math.Max(SelectionAnchor.Row, row);
			ActiveCell = new CellPosition(row, 0);
			CurrentSelection = new CellRegion(startRow, 0, endRow, lastCol);
		}
		else
		{
			ActiveCell = new CellPosition(row, 0);
			SelectionAnchor = ActiveCell;
			CurrentSelection = new CellRegion(row, 0, row, lastCol);
		}

		NotifyStateChanged();
	}

	public bool IsSelected(int row, int col) =>
		CurrentSelection?.Contains(new CellPosition(row, col)) == true;

	public bool IsActive(int row, int col) =>
		ActiveCell.Row == row && ActiveCell.Col == col;

	public bool IsColumnHeaderSelected(int col)
	{
		if (CurrentSelection is not { } sel || Model.RowCount == 0)
		{
			return false;
		}

		var n = sel.Normalize();
		return n.StartCol <= col && col <= n.EndCol
		                         && n.StartRow == 0 && n.EndRow == Model.RowCount - 1;
	}

	public bool IsRowHeaderSelected(int row)
	{
		if (CurrentSelection is not { } sel || Model.ColumnCount == 0)
		{
			return false;
		}

		var n = sel.Normalize();
		return n.StartRow <= row && row <= n.EndRow
		                         && n.StartCol == 0 && n.EndCol == Model.ColumnCount - 1;
	}

	public CellRegion GetEffectiveSelection()
	{
		if (CurrentSelection is { } sel)
		{
			return sel.Normalize();
		}

		return new CellRegion(ActiveCell.Row, ActiveCell.Col, ActiveCell.Row, ActiveCell.Col);
	}

	private void AdjustSelectionAfterRowInsert(int index)
	{
		ActiveCell = ShiftRowDown(ActiveCell, index);
		SelectionAnchor = ShiftRowDown(SelectionAnchor, index);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			var start = n.StartRow >= index ? n.StartRow + 1 : n.StartRow;
			var end = n.EndRow >= index ? n.EndRow + 1 : n.EndRow;
			CurrentSelection = new CellRegion(start, n.StartCol, end, n.EndCol);
		}
	}

	private void AdjustSelectionAfterRowDelete(int index) =>
		AdjustSelectionAfterRowsDelete(index, index);

	private void AdjustSelectionAfterRowsDelete(int startRow, int endRow)
	{
		var count = endRow - startRow + 1;
		ActiveCell = ShiftRowUpAfterRangeDelete(ActiveCell, startRow, endRow);
		SelectionAnchor = ShiftRowUpAfterRangeDelete(SelectionAnchor, startRow, endRow);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			if (n.StartRow >= startRow && n.EndRow <= endRow)
			{
				var row = Math.Min(startRow, Model.RowCount - 1);
				CurrentSelection = new CellRegion(row, n.StartCol, row, n.EndCol);
				ActiveCell = new CellPosition(row, Math.Clamp(ActiveCell.Col, n.StartCol, n.EndCol));
				SelectionAnchor = ActiveCell;
			}
			else
			{
				var start = n.StartRow;
				var end = n.EndRow;

				if (start > endRow)
				{
					start -= count;
				}
				else if (start >= startRow)
				{
					start = startRow;
				}

				if (end > endRow)
				{
					end -= count;
				}
				else if (end >= startRow)
				{
					end = startRow - 1;
				}

				if (end < start)
				{
					var row = Math.Min(startRow, Model.RowCount - 1);
					start = end = row;
				}

				start = Math.Clamp(start, 0, Model.RowCount - 1);
				end = Math.Clamp(end, 0, Model.RowCount - 1);
				CurrentSelection = new CellRegion(start, n.StartCol, end, n.EndCol);
			}
		}

		ClampSelectionToGrid();
	}

	private void AdjustSelectionAfterColumnInsert(int index)
	{
		ActiveCell = ShiftColRight(ActiveCell, index);
		SelectionAnchor = ShiftColRight(SelectionAnchor, index);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			var start = n.StartCol >= index ? n.StartCol + 1 : n.StartCol;
			var end = n.EndCol >= index ? n.EndCol + 1 : n.EndCol;
			CurrentSelection = new CellRegion(n.StartRow, start, n.EndRow, end);
		}
	}

	private void AdjustSelectionAfterColumnDelete(int index) =>
		AdjustSelectionAfterColumnsDelete(index, index);

	private void AdjustSelectionAfterColumnsDelete(int startCol, int endCol)
	{
		var count = endCol - startCol + 1;
		ActiveCell = ShiftColLeftAfterRangeDelete(ActiveCell, startCol, endCol);
		SelectionAnchor = ShiftColLeftAfterRangeDelete(SelectionAnchor, startCol, endCol);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			if (n.StartCol >= startCol && n.EndCol <= endCol)
			{
				var col = Math.Min(startCol, Model.ColumnCount - 1);
				CurrentSelection = new CellRegion(n.StartRow, col, n.EndRow, col);
				ActiveCell = new CellPosition(Math.Clamp(ActiveCell.Row, n.StartRow, n.EndRow), col);
				SelectionAnchor = ActiveCell;
			}
			else
			{
				var start = n.StartCol;
				var end = n.EndCol;

				if (start > endCol)
				{
					start -= count;
				}
				else if (start >= startCol)
				{
					start = startCol;
				}

				if (end > endCol)
				{
					end -= count;
				}
				else if (end >= startCol)
				{
					end = startCol - 1;
				}

				if (end < start)
				{
					var col = Math.Min(startCol, Model.ColumnCount - 1);
					start = end = col;
				}

				start = Math.Clamp(start, 0, Model.ColumnCount - 1);
				end = Math.Clamp(end, 0, Model.ColumnCount - 1);
				CurrentSelection = new CellRegion(n.StartRow, start, n.EndRow, end);
			}
		}

		ClampSelectionToGrid();
	}

	private static CellPosition ShiftRowDown(CellPosition pos, int index) =>
		pos.Row >= index ? new CellPosition(pos.Row + 1, pos.Col) : pos;

	private CellPosition ShiftRowUpAfterDelete(CellPosition pos, int index) =>
		ShiftRowUpAfterRangeDelete(pos, index, index);

	private CellPosition ShiftRowUpAfterRangeDelete(CellPosition pos, int startRow, int endRow)
	{
		if (pos.Row > endRow)
		{
			var count = endRow - startRow + 1;
			return new CellPosition(pos.Row - count, pos.Col);
		}

		if (pos.Row >= startRow)
		{
			return new CellPosition(Math.Min(startRow, Model.RowCount - 1), pos.Col);
		}

		return pos;
	}

	private static CellPosition ShiftColRight(CellPosition pos, int index) =>
		pos.Col >= index ? new CellPosition(pos.Row, pos.Col + 1) : pos;

	private CellPosition ShiftColLeftAfterDelete(CellPosition pos, int index) =>
		ShiftColLeftAfterRangeDelete(pos, index, index);

	private CellPosition ShiftColLeftAfterRangeDelete(CellPosition pos, int startCol, int endCol)
	{
		if (pos.Col > endCol)
		{
			var count = endCol - startCol + 1;
			return new CellPosition(pos.Row, pos.Col - count);
		}

		if (pos.Col >= startCol)
		{
			return new CellPosition(pos.Row, Math.Min(startCol, Model.ColumnCount - 1));
		}

		return pos;
	}

	private void ClampSelectionToGrid()
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		var row = Math.Clamp(ActiveCell.Row, 0, Model.RowCount - 1);
		var col = Math.Clamp(ActiveCell.Col, 0, Model.ColumnCount - 1);
		ActiveCell = new CellPosition(row, col);

		var ar = Math.Clamp(SelectionAnchor.Row, 0, Model.RowCount - 1);
		var ac = Math.Clamp(SelectionAnchor.Col, 0, Model.ColumnCount - 1);
		SelectionAnchor = new CellPosition(ar, ac);

		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			CurrentSelection = new CellRegion(
				Math.Clamp(n.StartRow, 0, Model.RowCount - 1),
				Math.Clamp(n.StartCol, 0, Model.ColumnCount - 1),
				Math.Clamp(n.EndRow, 0, Model.RowCount - 1),
				Math.Clamp(n.EndCol, 0, Model.ColumnCount - 1));
		}
	}
}
