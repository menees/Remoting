namespace Menees.Remoting.Json;

#region Using Directives

using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

internal sealed class ScalarObjectConverter : JsonConverter<object?>
{
	#region Public Methods

	/// <inheritdoc/>
	public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		object? result = reader.TokenType switch
		{
			JsonTokenType.True => true,
			JsonTokenType.False => false,
			JsonTokenType.String => reader.GetString(),
			JsonTokenType.Number => ReadNumber(ref reader),

			// Recursively handle arrays and objects.
			JsonTokenType.StartArray => JsonSerializer.Deserialize<object?[]>(ref reader, options),
			JsonTokenType.StartObject => JsonSerializer.Deserialize<Dictionary<string, object?>>(ref reader, options),

			// Note: JsonTokenType.Null is already handled by the reader.
			_ => throw new ArgumentException($"Unsupported token type: {reader.TokenType}"),
		};

		return result;
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
	{
		// The writer already handles a null value (as the null literal).
		if (value != null)
		{
			// We'll get a stack overflow if we don't handle a base (non-derived) object instance ourselves.
			Type inputType = value.GetType();
			if (inputType == typeof(object))
			{
				writer.WriteStartObject();
				writer.WriteEndObject();
			}
			else
			{
				// .NET only calls us for scalar object values (e.g., an int), so we'll defer to the default handling.
				JsonSerializer.Serialize(writer, value, inputType, options);
			}
		}
	}

	#endregion

	#region Private Methods

	private static object ReadNumber(ref Utf8JsonReader reader)
	{
		object result;

		if (reader.TryGetInt32(out int intValue))
		{
			result = intValue;
		}
		else if (reader.TryGetDecimal(out decimal decimalValue))
		{
			// There are several reasons to prefer decimal over double.
			// https://github.com/dotnet/runtime/issues/29960#issuecomment-877349847
			result = decimalValue;
		}
		else
		{
			// JavaScript's Number type is defined as a double, so we might see values in scientific notation.
			// JSON doesn't put a limit on the integral or fractional digits, but double will parse it as an approximation.
			result = reader.GetDouble();
		}

		return result;
	}

	#endregion
}
