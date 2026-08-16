using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	[JSInvokable]
	public Task OnSelectionDragBegin(int row, int col, bool shiftKey)
	{
		if (_isEditing)
		{
			CommitEdit();
		}

		_isSelecting = true;
		Context.SetActiveCell(row, col, shiftKey, notify: false);
		_ = FocusRootAsync();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnDropdownCellActivate(int row, int col, bool shiftKey)
	{
		if (_isEditing)
		{
			CommitEdit();
		}

		_isSelecting = false;
		Context.SetActiveCell(row, col, shiftKey);
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnSelectionDragEnd(int row, int col)
	{
		_isSelecting = false;
		Context.UpdateSelectionTo(row, col, notify: true);
		_ = FocusRootAsync();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnFillDragBegin()
	{
		Context.StartDragFill(notify: false);
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnFillDragEnd(int row, int col)
	{
		if (!Context.IsDragFilling)
		{
			Context.StartDragFill(notify: false);
		}

		Context.UpdateDragFillPreview(row, col, notify: false);
		Context.EndDragFill();
		return Task.CompletedTask;
	}

	private bool _clearResizeClassAfterRender;

	[JSInvokable]
	public Task OnColumnResizeBegin(int columnIndex)
	{
		_isResizingCol = true;
		_isSelecting = false;
		Context.BeginColumnResizeGesture();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnColumnResizeEnd(int columnIndex, int width)
	{
		Context.SetColumnWidth(columnIndex, width, notify: false);
		_isResizingCol = true;
		_clearResizeClassAfterRender = true;
		RecomputeVisibleRange();
		return InvokeAsync(StateHasChanged);
	}

	[JSInvokable]
	public Task OnRowResizeBegin(int rowIndex)
	{
		_isResizingRow = true;
		_isSelecting = false;
		Context.BeginRowResizeGesture();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnRowResizeEnd(int rowIndex, int height)
	{
		Context.SetRowHeight(rowIndex, height, notify: false);
		_isResizingRow = true;
		_clearResizeClassAfterRender = true;
		RecomputeVisibleRange();
		return InvokeAsync(StateHasChanged);
	}
}
