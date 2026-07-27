using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor(IJSRuntime _jsRuntime) : IAsyncDisposable
{
	[Parameter] public TableDataModel? Model { get; set; }
	[Parameter] public EventCallback<TableDataModel> ModelChanged { get; set; }
	[Parameter] public ITableValidator? Validator { get; set; }
	[Parameter] public bool ShowToolbar { get; set; }
	[Parameter] public int ViewportOverscan { get; set; } = 4;
	private SheetContext Context { get; set; } = null!;
	private ElementReference _rootRef;
	private ElementReference _viewportRef;
	private ElementReference _editorRef;
	private ElementReference _headerEditorRef;
	private bool _isSelecting;
	private bool _isResizingCol;
	private bool _isResizingRow;
	private int? _pressedColHeader;
	private int? _pressedRowHeader;
	private double _scrollLeft;
	private double _scrollTop;
	private double _viewportWidth = 800;
	private double _viewportHeight = 500;
	private int _firstRow;
	private int _lastRow;
	private int _firstCol;
	private int _lastCol;
	private DotNetObjectReference<MentorLakeTableEditor>? _dotNetRef;
	private bool _jsReady;
	private bool _disposed;
	private IJSObjectReference? _module;
	private IJSObjectReference? _instance;


	protected override void OnInitialized()
	{
		Context = new SheetContext(Model, addSampleIfEmpty: true);
		Context.SetValidator(Validator);
		WireContext(Context);
		RecomputeVisibleRange();
	}

	protected override void OnParametersSet()
	{
		bool modelChanged = Model is not null && !ReferenceEquals(Context.Model, Model);
		bool validatorChanged = !ReferenceEquals(Context.Validator, Validator);

		if (modelChanged)
		{
			UnwireContext(Context);
			Context = new SheetContext(Model, addSampleIfEmpty: false);
			Context.SetValidator(Validator);
			WireContext(Context);
			RecomputeVisibleRange();
		}
		else if (validatorChanged)
		{
			Context.SetValidator(Validator);
		}
	}
	private void WireContext(SheetContext ctx)
	{
		ctx.StateChanged += OnContextChanged;
		ctx.DataChanged += OnDataChanged;
	}

	private void UnwireContext(SheetContext ctx)
	{
		ctx.StateChanged -= OnContextChanged;
		ctx.DataChanged -= OnDataChanged;
	}

	private void OnContextChanged() => _ = InvokeAsync(StateHasChanged);

	private void OnDataChanged() => _ = InvokeAsync(async () =>
	{
		if (ModelChanged.HasDelegate)
		{
			await ModelChanged.InvokeAsync(Context.Model);
		}

		StateHasChanged();
	});
	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;
		_disposed = true;

		UnwireContext(Context);
		_dotNetRef?.Dispose();

		if (_instance is not null)
		{
			try
			{
				await _instance.InvokeVoidAsync("dispose");
				await _instance.DisposeAsync();
			}
			catch (JSDisconnectedException)
			{

			}

			_instance = null;
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
