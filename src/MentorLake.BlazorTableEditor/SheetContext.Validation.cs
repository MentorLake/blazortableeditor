using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	private ITableValidator _validator;
	private IReadOnlyDictionary<string, IReadOnlyList<ValidValueOption>> _columnValidValues;
	private readonly Dictionary<CellPosition, string> _validationErrors = new();
	private static readonly IReadOnlyList<ValidValueOption> EmptyValidValues = Array.Empty<ValidValueOption>();

	public IReadOnlyDictionary<CellPosition, string> ValidationErrors => _validationErrors;

	public ITableValidator Validator => _validator;

	public IReadOnlyDictionary<string, IReadOnlyList<ValidValueOption>> ColumnValidValues => _columnValidValues;

	public void SetValidator(ITableValidator validator)
	{
		_validator = validator;
		Revalidate();
	}

	public void SetColumnValidValues(IReadOnlyDictionary<string, IReadOnlyList<ValidValueOption>> columnValidValues)
	{
		_columnValidValues = columnValidValues;
		Revalidate();
	}

	public bool HasError(int row, int col) =>
		_validationErrors.ContainsKey(new CellPosition(row, col));

	public string GetError(int row, int col)
	{
		_validationErrors.TryGetValue(new CellPosition(row, col), out var msg);
		return msg;
	}

	public IReadOnlyList<ValidValueOption> GetValidValuesForColumn(int col)
	{
		if (_columnValidValues is null || col < 0 || col >= Model.ColumnCount)
		{
			return EmptyValidValues;
		}

		var header = Model.ColumnHeaders[col];
		if (string.IsNullOrEmpty(header))
		{
			return EmptyValidValues;
		}

		foreach (var kvp in _columnValidValues)
		{
			if (string.Equals(kvp.Key, header, StringComparison.OrdinalIgnoreCase))
			{
				return kvp.Value ?? EmptyValidValues;
			}
		}

		return EmptyValidValues;
	}

	public bool HasValidValuesForColumn(int col)
	{
		var values = GetValidValuesForColumn(col);
		return values.Count > 0;
	}

	public string ResolveValidValueDisplay(int col, string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value ?? string.Empty;
		}

		var options = GetValidValuesForColumn(col);
		for (var i = 0; i < options.Count; i++)
		{
			if (string.Equals(options[i].Value, value, StringComparison.Ordinal))
			{
				return options[i].Display;
			}
		}

		return value;
	}

	private void Revalidate()
	{
		ApplyValidation();
		NotifyStateChanged();
	}

	private void RevalidateInternal()
	{
		ApplyValidation();
	}

	private void ApplyValidation()
	{
		_validationErrors.Clear();
		ApplyColumnValidValueErrors();

		if (_validator is null)
		{
			return;
		}

		var errors = _validator.Validate(Model);
		if (errors is null)
		{
			return;
		}

		foreach (var kvp in errors)
		{
			if (kvp.Key.IsValid && !string.IsNullOrEmpty(kvp.Value))
			{
				_validationErrors[kvp.Key] = kvp.Value;
			}
		}
	}

	private void ApplyColumnValidValueErrors()
	{
		if (_columnValidValues is null || _columnValidValues.Count == 0)
		{
			return;
		}

		for (var c = 0; c < Model.ColumnCount; c++)
		{
			var validValues = GetValidValuesForColumn(c);
			if (validValues.Count == 0)
			{
				continue;
			}

			for (var r = 0; r < Model.RowCount; r++)
			{
				var cell = Model.GetCell(r, c);
				if (cell?.Value is null)
				{
					continue;
				}

				var text = cell.Value.ToString() ?? string.Empty;
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}

				if (!ContainsValidValue(validValues, text))
				{
					_validationErrors[new CellPosition(r, c)] = "Value is not in the list of allowed values.";
				}
			}
		}
	}

	private static bool ContainsValidValue(IReadOnlyList<ValidValueOption> validValues, string text)
	{
		for (var i = 0; i < validValues.Count; i++)
		{
			if (string.Equals(validValues[i].Value, text, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}
}
