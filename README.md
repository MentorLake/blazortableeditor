# BlazorTableEditor

An AI-generated high-performance, Excel-like table editor component library for Blazor.

## Features

- **Virtualized rendering** — Handles thousands of cells efficiently.
- **Full keyboard support** — Arrow keys, Shift+selection, Enter/F2 edit, Tab, Delete, Escape.
- **Undo / Redo** — Ctrl+Z (undo), Ctrl+Y or Ctrl+Shift+Z (redo), toolbar buttons, and context menu.
- **Validation** — Pluggable `ITableValidator` interface to mark cells as invalid; invalid cells receive error styling and show messages on hover.
- **Mouse interactions** — Click/drag selection, double-click edit, column/row resize, drag-fill handle.
- **Clipboard** — Copy, Cut, Paste with system clipboard (TSV) + internal buffer.
- **CSV serialization** — `TableDataModel.ToCsv` / `FromCsv`.
- **JSON serialization** — `TableDataModel.ToJson` / `FromJson`.
- **Context menu** — Right-click for insert/delete row/column + clipboard actions.
- **Header selection** — Click column header to select whole column, row number to select whole row.
- **Styling** — Button-like headers, selection highlighting, fill preview, marching ants clipboard indicator.

## Installation

```bash
dotnet add package MentorLake.BlazorTableEditor
```

## Usage

```razor
@using MentorLake.BlazorTableEditor

<TableEditor Model="myModel" />
```

### Basic setup in a page/component

```razor
@page "/editor"
@using MentorLake.BlazorTableEditor.Models

<TableEditor Model="@model" ModelChanged="OnModelChanged" />

@code {
    private TableDataModel model = new TableDataModel(50, 10);

    protected override void OnInitialized()
    {
        model.AddSampleData();
    }

    private void OnModelChanged(TableDataModel updated)
    {
        model = updated;
    }
}
```

## CSV

- `model.ToCsv(includeHeaders: false)` — export data rows only.
- `TableDataModel.FromCsv(csvText, firstRowIsHeader: true)` — import.

The component exposes `ExportCsvAsync` / file-based import in its toolbar when used directly.

## Styling

Override styles by targeting `.bte-root`, `.bte-cell`, `.bte-col-header`, etc.

## License

MIT
