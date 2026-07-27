using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private void OnKeyDown(KeyboardEventArgs e)
	{
		if (_isEditing)
		{
			return;
		}

		if (e.CtrlKey || e.MetaKey)
		{
			switch (e.Key.ToLowerInvariant())
			{
				case "c":
					_ = CopyAsync();
					return;
				case "x":
					_ = CutAsync();
					return;
				case "v":
					_ = PasteAsync();
					return;
				case "z":
					if (e.ShiftKey)
						Redo();
					else
						Undo();
					return;
				case "y":
					Redo();
					return;
			}
		}

		switch (e.Key)
		{
			case "ArrowRight":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + 1, e.ShiftKey);
				break;
			case "ArrowLeft":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col - 1, e.ShiftKey);
				break;
			case "ArrowDown":
				Context.SetActiveCell(Context.ActiveCell.Row + 1, Context.ActiveCell.Col, e.ShiftKey);
				break;
			case "ArrowUp":
				Context.SetActiveCell(Context.ActiveCell.Row - 1, Context.ActiveCell.Col, e.ShiftKey);
				break;
			case "Enter":
			case "F2":
				BeginEdit(Context.ActiveCell.Row, Context.ActiveCell.Col);
				break;
			case "Escape":
				if (_contextMenuOpen)
				{
					CloseContextMenu();
				}
				else if (_clipboardMode != ClipboardVisualMode.None)
				{
					ClearClipboardVisual();
				}
				else
				{
					Context.ClearSelection();
				}

				break;
			case "Delete":
			case "Backspace":
				Context.ClearSelectionValues();
				break;
			case "Tab":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + (e.ShiftKey ? -1 : 1));
				break;
			default:
				if (e.Key.Length == 1 && !e.CtrlKey && !e.AltKey && !e.MetaKey)
				{
					_editValue = e.Key;
					_editPos = Context.ActiveCell;
					_headerEditKind = HeaderEditKind.None;
					_headerEditIndex = -1;
					_isEditing = true;
					StateHasChanged();
				}

				break;
		}
	}
}
