using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private ClipboardGrid _internalClipboard = ClipboardGrid.Empty;
	private CellRegion? _clipboardSource;
	private ClipboardVisualMode _clipboardMode = ClipboardVisualMode.None;

	private enum ClipboardVisualMode
	{
		None,
		Copy,
		Cut
	}

	private bool IsInClipboardSource(int row, int col) =>
		_clipboardSource?.Contains(new CellPosition(row, col)) == true;

	private string GetClipboardOverlayClass() =>
		_clipboardMode == ClipboardVisualMode.Cut
			? "bte-clipboard-source is-cut"
			: "bte-clipboard-source is-copy";

	private string GetClipboardBadgeText() =>
		_clipboardMode == ClipboardVisualMode.Cut ? "CUT" : "COPIED";

	private void SetClipboardVisual(CellRegion region, ClipboardVisualMode mode)
	{
		_clipboardSource = region.Normalize();
		_clipboardMode = mode;
		StateHasChanged();
	}

	private void ClearClipboardVisual()
	{
		if (_clipboardMode == ClipboardVisualMode.None && _clipboardSource is null)
		{
			return;
		}

		_clipboardSource = null;
		_clipboardMode = ClipboardVisualMode.None;
		StateHasChanged();
	}

	private async Task ContextCutAsync()
	{
		CloseContextMenu();
		await CutAsync();
	}

	private async Task ContextCopyAsync()
	{
		CloseContextMenu();
		await CopyAsync();
	}

	private async Task ContextPasteAsync()
	{
		CloseContextMenu();
		await PasteAsync();
	}

	private async Task CopyAsync()
	{
		var region = Context.GetEffectiveSelection();
		_internalClipboard = Context.CopySelection();
		SetClipboardVisual(region, ClipboardVisualMode.Copy);
		await WriteClipboardTextAsync(_internalClipboard.ToTsv());
		await FocusRootAsync();
	}

	private async Task CutAsync()
	{
		var region = Context.GetEffectiveSelection();
		_internalClipboard = Context.CutSelection();
		SetClipboardVisual(region, ClipboardVisualMode.Cut);
		await WriteClipboardTextAsync(_internalClipboard.ToTsv());
		await FocusRootAsync();
	}

	private async Task PasteAsync()
	{
		var text = await ReadClipboardTextAsync();
		ClipboardGrid grid;

		if (!string.IsNullOrEmpty(text))
		{
			grid = ClipboardGrid.FromTsv(text);
			if (!grid.IsEmpty)
			{
				_internalClipboard = grid;
			}
		}

		if (_internalClipboard.IsEmpty)
		{
			return;
		}

		Context.PasteClipboard(_internalClipboard);
		ClearClipboardVisual();
		RecomputeVisibleRange();
		await FocusRootAsync();
		StateHasChanged();
	}
}
