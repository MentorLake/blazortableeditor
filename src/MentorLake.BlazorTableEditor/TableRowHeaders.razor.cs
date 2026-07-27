using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class TableRowHeaders
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public int FirstRow { get; set; }
	[Parameter] public int LastRow { get; set; }
	[Parameter] public int? PressedRowHeader { get; set; }
	[Parameter] public EventCallback<(int Row, MouseEventArgs Mouse)> OnRowMouseDown { get; set; }
	[Parameter] public EventCallback<MouseEventArgs> OnHeaderMouseUp { get; set; }
	[Parameter] public EventCallback<(int Row, MouseEventArgs Mouse)> OnContextMenu { get; set; }

	private Task OnRowMouseDownAsync(int row, MouseEventArgs e) =>
		OnRowMouseDown.InvokeAsync((row, e));

	private Task OnHeaderMouseUpAsync(MouseEventArgs e) =>
		OnHeaderMouseUp.InvokeAsync(e);

	private Task OnContextMenuAsync(int row, MouseEventArgs e) =>
		OnContextMenu.InvokeAsync((row, e));
}
