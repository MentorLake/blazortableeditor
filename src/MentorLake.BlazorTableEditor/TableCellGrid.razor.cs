using MentorLake.BlazorTableEditor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class TableCellGrid
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public int FirstRow { get; set; }
	[Parameter] public int LastRow { get; set; }
	[Parameter] public int FirstCol { get; set; }
	[Parameter] public int LastCol { get; set; }
	[Parameter] public CellRegion? ClipboardSource { get; set; }
	[Parameter] public ClipboardVisualMode ClipboardMode { get; set; } = ClipboardVisualMode.None;
	[Parameter] public EventCallback<(int Row, int Col, MouseEventArgs Mouse)> OnCellMouseDown { get; set; }
	[Parameter] public EventCallback<(int Row, int Col)> OnCellMouseEnter { get; set; }
	[Parameter] public EventCallback<(int Row, int Col)> OnDoubleClick { get; set; }
	[Parameter] public EventCallback<(int Row, int Col, MouseEventArgs Mouse)> OnContextMenu { get; set; }
	[Parameter] public RenderFragment ChildContent { get; set; }

	private bool IsInClipboardSource(int row, int col) =>
		ClipboardSource?.Contains(new CellPosition(row, col)) == true;

	private string GetClipboardOverlayClass() =>
		ClipboardMode == ClipboardVisualMode.Cut
			? "bte-clipboard-source is-cut"
			: "bte-clipboard-source is-copy";

	private string GetClipboardBadgeText() =>
		ClipboardMode == ClipboardVisualMode.Cut ? "CUT" : "COPIED";

	private static string BuildCellClass(bool isActive, bool isSelected, bool inFill, bool inClipboard, ClipboardVisualMode mode, bool hasError = false)
	{
		var css = "bte-cell";
		if (isActive) css += " is-active";
		if (isSelected) css += " is-selected";
		if (inFill) css += " is-fill";
		if (inClipboard && mode == ClipboardVisualMode.Copy) css += " is-copied";
		if (inClipboard && mode == ClipboardVisualMode.Cut) css += " is-cut";
		if (hasError) css += " is-error";
		return css;
	}

	private static string BuildCellStyle(int left, int top, int width, int height, CellValue cell)
	{
		var style = $"left:{left}px;top:{top}px;width:{width}px;height:{height}px;";
		if (!string.IsNullOrEmpty(cell?.BackgroundColor) && cell.BackgroundColor != "#ffffff")
		{
			style += $"background:{cell.BackgroundColor};";
		}

		if (!string.IsNullOrEmpty(cell?.TextColor) && cell.TextColor != "#000000")
		{
			style += $"color:{cell.TextColor};";
		}

		return style;
	}

	private string GetRegionBox(CellRegion region)
	{
		var s = region.Normalize();
		var left = Sheet.GetColumnLeft(s.StartCol);
		var top = Sheet.GetRowTop(s.StartRow);
		var right = Sheet.GetColumnLeft(s.EndCol) + Sheet.GetColumnWidth(s.EndCol);
		var bottom = Sheet.GetRowTop(s.EndRow) + Sheet.GetRowHeight(s.EndRow);
		return $"left:{left}px;top:{top}px;width:{right - left}px;height:{bottom - top}px;";
	}

	private Task OnCellMouseDownAsync(int row, int col, MouseEventArgs e) =>
		OnCellMouseDown.InvokeAsync((row, col, e));

	private Task OnCellMouseEnterAsync(int row, int col) =>
		OnCellMouseEnter.InvokeAsync((row, col));

	private Task OnDoubleClickAsync(int row, int col) =>
		OnDoubleClick.InvokeAsync((row, col));

	private Task OnContextMenuAsync(int row, int col, MouseEventArgs e) =>
		OnContextMenu.InvokeAsync((row, col, e));
}
