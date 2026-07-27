using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!_jsReady)
		{
			await EnsureJsInitializedAsync();
		}

		if (_clearResizeClassAfterRender)
		{
			_clearResizeClassAfterRender = false;
			_isResizingCol = false;
			_isResizingRow = false;
			if (_instance is not null)
			{
				try
				{
					await _instance.InvokeVoidAsync("clearResizeClasses");
				}
				catch
				{
				}
			}

			await InvokeAsync(StateHasChanged);
		}
	}

	private void RecomputeVisibleRange()
	{
		var rowCount = Context.Model.RowCount;
		var colCount = Context.Model.ColumnCount;
		if (rowCount == 0 || colCount == 0)
		{
			_firstRow = _lastRow = _firstCol = _lastCol = 0;
			return;
		}

		var bodyScrollTop = _scrollTop;
		var bodyViewHeight = Math.Max(1, _viewportHeight - SheetContext.ColumnHeaderHeight);
		var bodyScrollLeft = _scrollLeft;
		var bodyViewWidth = Math.Max(1, _viewportWidth - SheetContext.RowHeaderWidth);

		var y = 0;
		_firstRow = 0;
		for (var r = 0; r < rowCount; r++)
		{
			var h = Context.GetRowHeight(r);
			if (y + h > bodyScrollTop)
			{
				_firstRow = r;
				break;
			}

			y += h;
			_firstRow = r;
		}

		_firstRow = Math.Max(0, _firstRow - ViewportOverscan);

		var visibleBottom = bodyScrollTop + bodyViewHeight;
		y = Context.GetRowTop(_firstRow);
		_lastRow = _firstRow;
		for (var r = _firstRow; r < rowCount; r++)
		{
			y += Context.GetRowHeight(r);
			_lastRow = r;
			if (y >= visibleBottom)
			{
				break;
			}
		}

		_lastRow = Math.Min(rowCount - 1, _lastRow + ViewportOverscan);

		var x = 0;
		_firstCol = 0;
		for (var c = 0; c < colCount; c++)
		{
			var w = Context.GetColumnWidth(c);
			if (x + w > bodyScrollLeft)
			{
				_firstCol = c;
				break;
			}

			x += w;
			_firstCol = c;
		}

		_firstCol = Math.Max(0, _firstCol - ViewportOverscan);

		var visibleRight = bodyScrollLeft + bodyViewWidth;
		x = Context.GetColumnLeft(_firstCol);
		_lastCol = _firstCol;
		for (var c = _firstCol; c < colCount; c++)
		{
			x += Context.GetColumnWidth(c);
			_lastCol = c;
			if (x >= visibleRight)
			{
				break;
			}
		}

		_lastCol = Math.Min(colCount - 1, _lastCol + ViewportOverscan);
	}
}
