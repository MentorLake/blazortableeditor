using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class TableColumnHeaders
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public int FirstCol { get; set; }
	[Parameter] public int LastCol { get; set; }
	[Parameter] public int? PressedColHeader { get; set; }
	[Parameter] public int? EditingColumnIndex { get; set; }
	[Parameter] public string EditValue { get; set; } = string.Empty;
	[Parameter] public EventCallback<string> EditValueChanged { get; set; }
	[Parameter] public EventCallback<(int Col, MouseEventArgs Mouse)> OnColMouseDown { get; set; }
	[Parameter] public EventCallback<MouseEventArgs> OnHeaderMouseUp { get; set; }
	[Parameter] public EventCallback<int> OnDoubleClick { get; set; }
	[Parameter] public EventCallback<(int Col, MouseEventArgs Mouse)> OnContextMenu { get; set; }
	[Parameter] public EventCallback OnCommit { get; set; }
	[Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }
	[Parameter] public EventCallback<(int Col, MouseEventArgs Mouse)> OnFilterClick { get; set; }

	private ElementReference _headerEditorRef;
	private bool _shouldFocus;
	private bool _wasEditing;
	private int _lastFocusedCol = -1;

	private bool IsEditingColumn(int col) =>
		EditingColumnIndex is { } index && index == col;

	protected override void OnParametersSet()
	{
		var editingCol = EditingColumnIndex ?? -1;
		var isActive = editingCol >= 0;
		if (isActive && (!_wasEditing || _lastFocusedCol != editingCol))
		{
			_shouldFocus = true;
			_lastFocusedCol = editingCol;
		}
		else if (!isActive)
		{
			_lastFocusedCol = -1;
		}

		_wasEditing = isActive;
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!_shouldFocus)
		{
			return;
		}

		_shouldFocus = false;
		try
		{
			await _headerEditorRef.FocusAsync();
		}
		catch
		{
		}
	}

	private Task OnColMouseDownAsync(int col, MouseEventArgs e) =>
		OnColMouseDown.InvokeAsync((col, e));

	private Task OnHeaderMouseUpAsync(MouseEventArgs e) =>
		OnHeaderMouseUp.InvokeAsync(e);

	private Task OnDoubleClickAsync(int col) =>
		OnDoubleClick.InvokeAsync(col);

	private Task OnContextMenuAsync(int col, MouseEventArgs e) =>
		OnContextMenu.InvokeAsync((col, e));

	private async Task OnEditInputAsync(ChangeEventArgs e)
	{
		var next = e.Value?.ToString() ?? string.Empty;
		EditValue = next;
		await EditValueChanged.InvokeAsync(next);
	}

	private Task OnEditorKeyDownAsync(KeyboardEventArgs e) =>
		OnKeyDown.InvokeAsync(e);

	private Task OnCommitAsync() => OnCommit.InvokeAsync();

	private Task OnFilterClickAsync(int col, MouseEventArgs e) =>
		OnFilterClick.InvokeAsync((col, e));
}
