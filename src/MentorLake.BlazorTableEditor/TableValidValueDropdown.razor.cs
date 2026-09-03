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
	private IReadOnlyList<ValidValueOption> _options = Array.Empty<ValidValueOption>();
	private string _currentValue = string.Empty;
	private bool _disposed;

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
		_ = InvokeAsync(StateHasChanged);
	}

	public async Task ShowAsync()
	{
		await EnsureModuleAsync();
		if (_disposed || _module is null)
		{
			return;
		}

		try
		{
			await _module.InvokeVoidAsync("showPopover", _popoverRef);
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
		}
		catch
		{
		}
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

	private async Task SelectAsync(string value)
	{
		await OnSelected.InvokeAsync(value ?? string.Empty);
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
