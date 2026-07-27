using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	public ClipboardGrid CopySelection()
	{
		var region = GetEffectiveSelection();
		var rows = region.Height;
		var cols = region.Width;
		var cells = new CellValue[rows, cols];

		for (var r = 0; r < rows; r++)
		{
			for (var c = 0; c < cols; c++)
			{
				var source = GetValue(region.StartRow + r, region.StartCol + c);
				cells[r, c] = source?.Clone();
			}
		}

		return new ClipboardGrid { Rows = rows, Cols = cols, Cells = cells };
	}

	public ClipboardGrid CutSelection()
	{
		var grid = CopySelection();
		ClearSelectionValues();
		return grid;
	}

	public CellRegion PasteClipboard(ClipboardGrid clipboard, bool tileToSelection = true)
	{
		if (clipboard.IsEmpty || Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return GetEffectiveSelection();
		}

		PushUndoSnapshot();

		var selection = GetEffectiveSelection();
		var startRow = selection.StartRow;
		var startCol = selection.StartCol;

		var pasteRows = clipboard.Rows;
		var pasteCols = clipboard.Cols;

		if (tileToSelection && (selection.Height > 1 || selection.Width > 1))
		{
			if (selection.Height >= clipboard.Rows && selection.Height % clipboard.Rows == 0)
			{
				pasteRows = selection.Height;
			}

			if (selection.Width >= clipboard.Cols && selection.Width % clipboard.Cols == 0)
			{
				pasteCols = selection.Width;
			}
		}

		pasteRows = Math.Min(pasteRows, Model.RowCount - startRow);
		pasteCols = Math.Min(pasteCols, Model.ColumnCount - startCol);

		for (var r = 0; r < pasteRows; r++)
		{
			for (var c = 0; c < pasteCols; c++)
			{
				var tr = startRow + r;
				var tc = startCol + c;
				var source = clipboard.Cells[r % clipboard.Rows, c % clipboard.Cols];
				if (source is null || source.Value is null)
				{
					Model.ClearCell(tr, tc);
				}
				else
				{
					Model.SetCell(tr, tc, source.Clone());
				}
			}
		}

		var pasted = new CellRegion(startRow, startCol, startRow + pasteRows - 1, startCol + pasteCols - 1);
		CurrentSelection = pasted;
		SelectionAnchor = new CellPosition(pasted.StartRow, pasted.StartCol);
		ActiveCell = new CellPosition(pasted.StartRow, pasted.StartCol);

		NotifyDataChanged();
		NotifyStateChanged();
		return pasted;
	}
}
