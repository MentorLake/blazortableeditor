namespace MentorLake.BlazorTableEditor.Models;

public sealed class ValidValueOption
{
	public ValidValueOption(string value)
		: this(value, null)
	{
	}

	public ValidValueOption(string value, string display)
	{
		Value = value ?? string.Empty;
		Display = string.IsNullOrEmpty(display) ? Value : display;
	}

	public string Value { get; }

	public string Display { get; }

	public static implicit operator ValidValueOption(string value) => new(value);

	public static ValidValueOption[] FromValues(params string[] values)
	{
		if (values is null || values.Length == 0)
		{
			return Array.Empty<ValidValueOption>();
		}

		var result = new ValidValueOption[values.Length];
		for (var i = 0; i < values.Length; i++)
		{
			result[i] = new ValidValueOption(values[i]);
		}

		return result;
	}
}
