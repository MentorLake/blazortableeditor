using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	private bool _isDragFilling;
	private CellRegion _dragFillSource;
	private CellRegion? _dragFillPreview;

	public CellRegion? DragFillPreview => _dragFillPreview;
	public bool IsDragFilling => _isDragFilling;

	public void StartDragFill(bool notify = true)
	{
		_dragFillSource = (CurrentSelection ?? new CellRegion(ActiveCell.Row, ActiveCell.Col, ActiveCell.Row, ActiveCell.Col)).Normalize();
		_isDragFilling = true;
		_dragFillPreview = _dragFillSource;
		if (notify)
		{
			NotifyStateChanged();
		}
	}

	public void UpdateDragFillPreview(int row, int col, bool notify = true)
	{
		if (!_isDragFilling)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		col = Math.Clamp(col, 0, Model.ColumnCount - 1);

		var source = _dragFillSource;
		int startRow = source.StartRow;
		int endRow = source.EndRow;
		int startCol = source.StartCol;
		int endCol = source.EndCol;

		if (row > source.EndRow)
		{
			endRow = row;
		}
		else if (row < source.StartRow)
		{
			startRow = row;
		}

		if (col > source.EndCol)
		{
			endCol = col;
		}
		else if (col < source.StartCol)
		{
			startCol = col;
		}

		_dragFillPreview = new CellRegion(startRow, startCol, endRow, endCol).Normalize();
		if (notify)
		{
			NotifyStateChanged();
		}
	}

	public CellRegion GetDragFillSource() => _dragFillSource;

	public void EndDragFill()
	{
		if (!_isDragFilling)
		{
			return;
		}

		if (_dragFillPreview is { } preview && !preview.Equals(_dragFillSource))
		{
			PushUndoSnapshot();
			ApplyDragFill(_dragFillSource, preview);
		}

		_isDragFilling = false;
		_dragFillPreview = null;
		NotifyStateChanged();
	}

	public void CancelDragFill()
	{
		_isDragFilling = false;
		_dragFillPreview = null;
		NotifyStateChanged();
	}

	private void ApplyDragFill(CellRegion source, CellRegion target)
	{
		source = source.Normalize();
		target = target.Normalize();

		if (source.Width <= 0 || source.Height <= 0)
		{
			return;
		}

		for (int tr = target.StartRow; tr <= target.EndRow; tr++)
		{
			for (int tc = target.StartCol; tc <= target.EndCol; tc++)
			{
				if (source.Contains(new CellPosition(tr, tc)))
				{
					continue;
				}

				int srcRowOffset = Math.Abs(tr - source.StartRow) % source.Height;
				int srcColOffset = Math.Abs(tc - source.StartCol) % source.Width;
				int sr = source.StartRow + srcRowOffset;
				int sc = source.StartCol + srcColOffset;

				var sourceValue = GetValue(sr, sc);
				if (sourceValue is not null)
				{
					Model.SetCell(tr, tc, new CellValue(sourceValue.Value) { Format = sourceValue.Format, BackgroundColor = sourceValue.BackgroundColor, TextColor = sourceValue.TextColor });
				}
				else
				{
					Model.ClearCell(tr, tc);
				}
			}
		}

		CurrentSelection = target;
		ActiveCell = new CellPosition(target.EndRow, target.EndCol);
		SelectionAnchor = new CellPosition(target.StartRow, target.StartCol);

		NotifyDataChanged();
		NotifyStateChanged();
	}
}
