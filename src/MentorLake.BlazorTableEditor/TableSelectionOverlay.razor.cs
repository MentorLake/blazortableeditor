using MentorLake.BlazorTableEditor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class TableSelectionOverlay
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public EventCallback<MouseEventArgs> OnFillHandleMouseDown { get; set; }

	private string GetRegionBox(CellRegion region)
	{
		var s = region.Normalize();
		var left = Sheet.GetColumnLeft(s.StartCol);
		var top = Sheet.GetRowTop(s.StartRow);
		var right = Sheet.GetColumnLeft(s.EndCol) + Sheet.GetColumnWidth(s.EndCol);
		var bottom = Sheet.GetRowTop(s.EndRow) + Sheet.GetRowHeight(s.EndRow);
		return $"left:{left}px;top:{top}px;width:{right - left}px;height:{bottom - top}px;";
	}
}
