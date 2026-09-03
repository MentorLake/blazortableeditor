using MentorLake.BlazorTableEditor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class TableValidValueDropdown : IAsyncDisposable
{
	[Inject] private IJSRuntime Js { get; set; }

	[Parameter] public string PopoverId { get; set; }
	[Parameter] public int MaxVisibleItems { get; set; } = 8;
	[Parameter] public EventCallback<string> OnSelected { get; set; }

	private ElementReference _popoverRef;
	private IJSObjectReference _module;
	private DotNetObjectReference<TableValidValueDropdown> _dotNetRef;
	private IReadOnlyList<ValidValueOption> _options = Array.Empty<ValidValueOption>();
	private string _currentValue = string.Empty;
	private List<string> _itemValues = new();
	private int _highlightIndex;
	private bool _isOpen;
	private bool _disposed;
	private bool _toggleBound;

	public bool IsOpen => _isOpen;

	private string DropdownStyle =>
		$"--bte-vv-max-visible:{Math.Max(1, MaxVisibleItems)};";

	public void SetContent(IReadOnlyList<ValidValueOption> options, string currentValue)
	{
		var nextOptions = options ?? Array.Empty<ValidValueOption>();
		var nextCurrent = currentValue ?? string.Empty;
		if (ReferenceEquals(_options, nextOptions)
		    && string.Equals(_currentValue, nextCurrent, StringComparison.Ordinal))
		{
			return;
		}

		_options = nextOptions;
		_currentValue = nextCurrent;
		RebuildItems();
		_ = InvokeAsync(StateHasChanged);
	}

	public async Task EnsureReadyAsync()
	{
		await EnsureModuleAsync();
		if (_disposed || _module is null)
		{
			return;
		}

		await BindToggleAsync();
	}

	public async Task ShowAsync()
	{
		await EnsureReadyAsync();
		if (_disposed || _module is null)
		{
			return;
		}

		try
		{
			var opened = await _module.InvokeAsync<bool>("showPopover", _popoverRef);
			if (opened)
			{
				_isOpen = true;
				EnsureHighlightInRange();
				await ScrollHighlightIntoViewAsync();
				await InvokeAsync(StateHasChanged);
			}
		}
		catch
		{
		}
	}

	public async Task HideAsync()
	{
		await EnsureModuleAsync();
		if (_disposed || _module is null)
		{
			return;
		}

		try
		{
			await _module.InvokeVoidAsync("hidePopover", _popoverRef);
			_isOpen = false;
			await InvokeAsync(StateHasChanged);
		}
		catch
		{
		}
	}

	public void MoveHighlight(int delta)
	{
		if (!_isOpen || _itemValues.Count == 0 || delta == 0)
		{
			return;
		}

		var count = _itemValues.Count;
		_highlightIndex = ((_highlightIndex + delta) % count + count) % count;
		_ = ScrollHighlightIntoViewAsync();
		_ = InvokeAsync(StateHasChanged);
	}

	public async Task CommitHighlightAsync()
	{
		if (!_isOpen || _itemValues.Count == 0)
		{
			return;
		}

		EnsureHighlightInRange();
		var value = _itemValues[_highlightIndex] ?? string.Empty;
		await SelectAsync(value);
	}

	[JSInvokable]
	public void OnPopoverToggle(string newState)
	{
		var open = string.Equals(newState, "open", StringComparison.Ordinal);
		if (_isOpen == open)
		{
			return;
		}

		_isOpen = open;
		if (open)
		{
			EnsureHighlightInRange();
		}

		_ = InvokeAsync(StateHasChanged);
	}

	private async Task EnsureModuleAsync()
	{
		if (_module is not null)
		{
			return;
		}

		try
		{
			_module = await Js.InvokeAsync<IJSObjectReference>(
				"import",
				$"./_content/MentorLake.BlazorTableEditor/{nameof(MentorLakeTableEditor)}.razor.js");
		}
		catch
		{
			_module = null;
		}
	}

	private async Task BindToggleAsync()
	{
		if (_toggleBound || _module is null)
		{
			return;
		}

		_dotNetRef ??= DotNetObjectReference.Create(this);
		try
		{
			await _module.InvokeVoidAsync("bindPopoverToggle", _popoverRef, _dotNetRef, "OnPopoverToggle");
			_toggleBound = true;
		}
		catch
		{
		}
	}

	private async Task ScrollHighlightIntoViewAsync()
	{
		if (_module is null || !_isOpen)
		{
			return;
		}

		try
		{
			await _module.InvokeVoidAsync("scrollPopoverOptionIntoView", _popoverRef, _highlightIndex);
		}
		catch
		{
		}
	}

	private void RebuildItems()
	{
		_itemValues = new List<string>();
		_itemValues.Add(string.Empty);

		var current = _currentValue ?? string.Empty;
		var currentInList = _options.Count > 0 && ContainsValue(_options, current);
		if (current.Length > 0 && !currentInList)
		{
			_itemValues.Add(current);
		}

		for (var i = 0; i < _options.Count; i++)
		{
			_itemValues.Add(_options[i].Value ?? string.Empty);
		}

		_highlightIndex = FindValueIndex(current);
		EnsureHighlightInRange();
	}

	private int FindValueIndex(string value)
	{
		var text = value ?? string.Empty;
		for (var i = 0; i < _itemValues.Count; i++)
		{
			if (string.Equals(_itemValues[i], text, StringComparison.Ordinal))
			{
				return i;
			}
		}

		return 0;
	}

	private void EnsureHighlightInRange()
	{
		if (_itemValues.Count == 0)
		{
			_highlightIndex = 0;
			return;
		}

		if (_highlightIndex < 0 || _highlightIndex >= _itemValues.Count)
		{
			_highlightIndex = FindValueIndex(_currentValue);
			if (_highlightIndex < 0 || _highlightIndex >= _itemValues.Count)
			{
				_highlightIndex = 0;
			}
		}
	}

	private async Task SelectAsync(string value)
	{
		await OnSelected.InvokeAsync(value ?? string.Empty);
		await HideAsync();
	}

	private static bool ContainsValue(IReadOnlyList<ValidValueOption> values, string text)
	{
		for (var i = 0; i < values.Count; i++)
		{
			if (string.Equals(values[i].Value, text, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isOpen = false;
		if (_dotNetRef is not null)
		{
			_dotNetRef.Dispose();
			_dotNetRef = null;
		}

		if (_module is not null)
		{
			try
			{
				await _module.DisposeAsync();
			}
			catch (JSDisconnectedException)
			{
			}

			_module = null;
		}
	}
}
