using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private void OnCellMouseDown(int row, int col, MouseEventArgs e)
	{
		if (_jsReady || e.Button != 0)
		{
			return;
		}

		if (_isEditing)
		{
			CommitEdit();
		}

		_isSelecting = true;
		Context.SetActiveCell(row, col, e.ShiftKey);
		_ = FocusRootAsync();
	}

	private void OnCellMouseEnter(int row, int col)
	{
		if (_jsReady)
		{
			return;
		}

		if (Context.IsDragFilling)
		{
			Context.UpdateDragFillPreview(row, col);
			return;
		}

		if (_isSelecting)
		{
			Context.UpdateSelectionTo(row, col);
		}
	}

	private void OnRootMouseLeave(MouseEventArgs e)
	{
		if (_isResizingCol || _isResizingRow || _jsReady)
		{
			return;
		}

		OnGlobalMouseUp(e);
	}

	private void OnGlobalMouseUp(MouseEventArgs e)
	{
		var hadPressedHeader = _pressedColHeader is not null || _pressedRowHeader is not null;
		if (hadPressedHeader)
		{
			_pressedColHeader = null;
			_pressedRowHeader = null;
		}

		if (_isResizingCol || _isResizingRow)
		{
			if (hadPressedHeader)
			{
				StateHasChanged();
			}

			return;
		}

		if (_jsReady)
		{
			if (hadPressedHeader)
			{
				StateHasChanged();
			}

			return;
		}

		if (Context.IsDragFilling)
		{
			Context.EndDragFill();
		}

		_isSelecting = false;

		if (hadPressedHeader)
		{
			StateHasChanged();
		}
	}


	private void OnCanvasMouseMove(MouseEventArgs e)
	{
	}


	private void OnColHeaderMouseDown(int col, MouseEventArgs e)
	{
		if (e.Button != 0)
		{
			return;
		}

		// Keep focus in the header editor when clicking the header being edited.
		if (IsEditingColumnHeader(col))
		{
			return;
		}

		if (_isEditing)
		{
			CommitEdit();
		}

		_pressedColHeader = col;
		_pressedRowHeader = null;
		Context.SelectColumn(col, e.ShiftKey);
		_isSelecting = false;
		_ = FocusRootAsync();
	}

	private void OnRowHeaderMouseDown(int row, MouseEventArgs e)
	{
		if (e.Button != 0)
		{
			return;
		}

		if (_isEditing)
		{
			CommitEdit();
		}

		_pressedRowHeader = row;
		_pressedColHeader = null;
		Context.SelectRow(row, e.ShiftKey);
		_isSelecting = false;
		_ = FocusRootAsync();
	}

	private void OnHeaderMouseUp(MouseEventArgs e)
	{
		if (_pressedColHeader is null && _pressedRowHeader is null)
		{
			return;
		}

		_pressedColHeader = null;
		_pressedRowHeader = null;
		StateHasChanged();
	}

	private void OnFillHandleMouseDown(MouseEventArgs e)
	{
		if (_jsReady || e.Button != 0)
		{
			return;
		}

		Context.StartDragFill();
	}
}
