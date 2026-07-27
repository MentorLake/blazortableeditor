using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private async Task EnsureJsInitializedAsync()
	{
		try
		{
			_module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./_content/MentorLake.BlazorTableEditor/{nameof(MentorLakeTableEditor)}.razor.js");
			_instance = await _module.InvokeAsync<IJSObjectReference>("createInstance");
			_dotNetRef ??= DotNetObjectReference.Create(this);
			var ok = await _instance.InvokeAsync<bool>("init", _viewportRef, _dotNetRef);
			_jsReady = ok;
			if (_jsReady)
			{
				await RefreshViewportMetricsAsync();
				StateHasChanged();
			}
		}
		catch (Exception e)
		{
			Console.WriteLine(e);

			_jsReady = false;
			if (_viewportWidth <= 0 || _viewportHeight <= 0)
			{
				_viewportWidth = 900;
				_viewportHeight = 520;
				RecomputeVisibleRange();
			}
		}
	}

	private async Task OnViewportScroll(EventArgs _)
	{
		if (IsContextMenuOpen)
		{
			CloseContextMenu();
		}

		if (!_jsReady)
		{
			await EnsureJsInitializedAsync();
		}

		await RefreshViewportMetricsAsync();
		StateHasChanged();
	}

	[JSInvokable]
	public Task OnViewportMetrics(double width, double height, double scrollLeft, double scrollTop)
	{
		_viewportWidth = Math.Max(1, width);
		_viewportHeight = Math.Max(1, height);
		_scrollLeft = Math.Max(0, scrollLeft);
		_scrollTop = Math.Max(0, scrollTop);
		RecomputeVisibleRange();
		return InvokeAsync(StateHasChanged);
	}

	private async Task RefreshViewportMetricsAsync()
	{
		if (!_jsReady)
		{
			return;
		}

		try
		{
			var metrics = await _instance.InvokeAsync<double[]>("getMetrics", _viewportRef);
			if (metrics is { Length: >= 4 })
			{
				_viewportWidth = Math.Max(1, metrics[0]);
				_viewportHeight = Math.Max(1, metrics[1]);
				_scrollLeft = Math.Max(0, metrics[2]);
				_scrollTop = Math.Max(0, metrics[3]);
				RecomputeVisibleRange();
			}
		}
		catch
		{
		}
	}

	private async Task WriteClipboardTextAsync(string text)
	{
		try
		{
			await _instance.InvokeVoidAsync("writeText", text);
		}
		catch
		{
		}
	}

	private async Task<string> ReadClipboardTextAsync()
	{
		try
		{
			return await _instance.InvokeAsync<string>("readText");
		}
		catch
		{
			return null;
		}
	}

	private async Task FocusRootAsync()
	{
		try
		{
			await _rootRef.FocusAsync();
		}
		catch
		{
		}
	}
}
