using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	private ITableValidator _validator;
	private readonly Dictionary<CellPosition, string> _validationErrors = new();

	public IReadOnlyDictionary<CellPosition, string> ValidationErrors => _validationErrors;

	public ITableValidator Validator => _validator;

	public void SetValidator(ITableValidator validator)
	{
		_validator = validator;
		Revalidate();
	}

	public bool HasError(int row, int col) =>
		_validationErrors.ContainsKey(new CellPosition(row, col));

	public string GetError(int row, int col)
	{
		_validationErrors.TryGetValue(new CellPosition(row, col), out var msg);
		return msg;
	}

	private void Revalidate()
	{
		_validationErrors.Clear();
		if (_validator is null)
		{
			NotifyStateChanged();
			return;
		}

		var errors = _validator.Validate(Model);
		if (errors is not null)
		{
			foreach (var kvp in errors)
			{
				if (kvp.Key.IsValid && !string.IsNullOrEmpty(kvp.Value))
				{
					_validationErrors[kvp.Key] = kvp.Value;
				}
			}
		}

		NotifyStateChanged();
	}

	private void RevalidateInternal()
	{
		if (_validator is not null)
		{
			_validationErrors.Clear();
			var errors = _validator.Validate(Model);
			if (errors is not null)
			{
				foreach (var kvp in errors)
				{
					if (kvp.Key.IsValid && !string.IsNullOrEmpty(kvp.Value))
					{
						_validationErrors[kvp.Key] = kvp.Value;
					}
				}
			}
		}
	}
}
