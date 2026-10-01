using Microsoft.AspNetCore.Components.Web;

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
					if (!ReadOnly)
					{
						_ = CutAsync();
					}
					return;
				case "v":
					if (!ReadOnly)
					{
						_ = PasteAsync();
					}
					return;
				case "z":
					if (!ReadOnly)
					{
						if (e.ShiftKey)
							Redo();
						else
							Undo();
					}
					return;
				case "y":
					if (!ReadOnly)
					{
						Redo();
					}
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
				Context.SetActiveCell(
					Context.FindNextVisibleRow(Context.ActiveCell.Row, 1),
					Context.ActiveCell.Col,
					e.ShiftKey);
				break;
			case "ArrowUp":
				Context.SetActiveCell(
					Context.FindNextVisibleRow(Context.ActiveCell.Row, -1),
					Context.ActiveCell.Col,
					e.ShiftKey);
				break;
			case "Enter":
			case "F2":
			case " ":
				if (!ReadOnly)
				{
					BeginEdit(Context.ActiveCell.Row, Context.ActiveCell.Col);
				}
				break;
			case "Escape":
				if (IsFilterPopupOpen)
				{
					CloseFilterPopup();
				}
				else if (IsContextMenuOpen)
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
				if (!ReadOnly)
				{
					Context.ClearSelectionValues();
				}
				break;
			case "Tab":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + (e.ShiftKey ? -1 : 1));
				break;
			default:
				if (ReadOnly)
				{
					break;
				}

				if (e.Key.Length == 1 && !e.CtrlKey && !e.AltKey && !e.MetaKey)
				{
					if (Context.HasValidValuesForColumn(Context.ActiveCell.Col))
					{
						break;
					}

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
