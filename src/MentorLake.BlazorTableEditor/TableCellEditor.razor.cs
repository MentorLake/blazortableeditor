using MentorLake.BlazorTableEditor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class TableCellEditor
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public bool IsEditing { get; set; }
	[Parameter] public CellPosition Position { get; set; } = CellPosition.Invalid;
	[Parameter] public string Value { get; set; } = string.Empty;
	[Parameter] public EventCallback<string> ValueChanged { get; set; }
	[Parameter] public EventCallback OnCommit { get; set; }
	[Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }

	private ElementReference _inputRef;
	private bool _shouldFocus;
	private bool _wasEditing;
	private CellPosition _lastFocusedPos = CellPosition.Invalid;

	protected override void OnParametersSet()
	{
		var isActive = IsEditing && Position.IsValid;
		if (isActive && (!_wasEditing || _lastFocusedPos != Position))
		{
			_shouldFocus = true;
			_lastFocusedPos = Position;
		}
		else if (!isActive)
		{
			_lastFocusedPos = CellPosition.Invalid;
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
			await _inputRef.FocusAsync();
		}
		catch
		{
		}
	}

	private async Task OnInputAsync(ChangeEventArgs e)
	{
		var next = e.Value?.ToString() ?? string.Empty;
		Value = next;
		await ValueChanged.InvokeAsync(next);
	}

	private Task OnBlur() => OnCommit.InvokeAsync();
}
