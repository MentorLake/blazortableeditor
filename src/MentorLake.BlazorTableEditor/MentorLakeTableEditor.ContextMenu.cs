using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{
	private bool _contextMenuOpen;
	private double _contextMenuX;
	private double _contextMenuY;
	private int _contextRow;
	private int _contextCol;
	private HeaderEditKind _contextHeaderKind = HeaderEditKind.None;

	private void OnRootContextMenu(MouseEventArgs e)
	{
		OpenContextMenu(e, Context.ActiveCell.Row, Context.ActiveCell.Col);
	}

	private void OpenContextMenu(MouseEventArgs e, int row, int col, bool selectCell = true, HeaderEditKind headerKind = HeaderEditKind.None)
	{
		if (_isEditing)
		{
			CommitEdit();
		}

		row = Math.Clamp(row, 0, Math.Max(0, Context.Model.RowCount - 1));
		col = Math.Clamp(col, 0, Math.Max(0, Context.Model.ColumnCount - 1));
		_contextRow = row;
		_contextCol = col;
		_contextHeaderKind = headerKind;

		if (selectCell && !Context.IsSelected(row, col))
		{
			Context.SetActiveCell(row, col);
		}

		_contextMenuX = e.ClientX;
		_contextMenuY = e.ClientY;
		_contextMenuOpen = true;
		_isSelecting = false;
		StateHasChanged();
	}

	private void CloseContextMenu()
	{
		if (!_contextMenuOpen)
		{
			return;
		}

		_contextMenuOpen = false;
		_contextHeaderKind = HeaderEditKind.None;
		StateHasChanged();
	}

	private bool CanRenameHeader => _contextHeaderKind == HeaderEditKind.Column;

	private void ContextRenameHeader()
	{
		if (!CanRenameHeader)
		{
			CloseContextMenu();
			return;
		}

		var index = _contextCol;
		_contextMenuOpen = false;
		_contextHeaderKind = HeaderEditKind.None;
		BeginHeaderEdit(HeaderEditKind.Column, index, selectHeader: false);
	}

	private bool CanDeleteRow => Context.Model.RowCount > 1;
	private bool CanDeleteColumn => Context.Model.ColumnCount > 1;

	private void ContextInsertColLeft()
	{
		Context.InsertColumn(_contextCol);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextInsertColRight()
	{
		Context.InsertColumn(_contextCol + 1);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextDeleteColumn()
	{
		if (!CanDeleteColumn)
		{
			CloseContextMenu();
			return;
		}

		Context.DeleteColumn(_contextCol);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextInsertRowAbove()
	{
		Context.InsertRow(_contextRow);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextInsertRowBelow()
	{
		Context.InsertRow(_contextRow + 1);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextDeleteRow()
	{
		if (!CanDeleteRow)
		{
			CloseContextMenu();
			return;
		}

		Context.DeleteRow(_contextRow);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

}
