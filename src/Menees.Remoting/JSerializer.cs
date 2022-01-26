﻿namespace Menees.Remoting;

#region Private Data Members

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

#endregion

internal sealed class JSerializer : ISerializer
{
	#region Private Data Members

	private static readonly Encoding SerilizerEncoding = Encoding.UTF8;

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		Converters =
		{
			// Serialize TimeSpan using its invariant "c" format (i.e., ToString()) instead of as a multi-property struct.
			JsonMetadataServices.TimeSpanConverter,

			// Serialize Enum as a field name string instead of as an int.
			new JsonStringEnumConverter(),

			// Deserialize scalar Object values by inferring a preferred type instead of always returning JsonElement.
			new ScalarObjectConverter(),
		},
	};

	#endregion

	#region Public Methods

	public object? Deserialize(byte[] serializedValue, Type returnType)
	{
		string json = SerilizerEncoding.GetString(serializedValue);
		object? result = JsonSerializer.Deserialize(json, returnType, SerializerOptions);
		return result;
	}

	public byte[] Serialize(object? value, Type valueType)
	{
		string json = JsonSerializer.Serialize(value, valueType, SerializerOptions);
		byte[] result = SerilizerEncoding.GetBytes(json);
		return result;
	}

	#endregion

	#region Private Types

	private sealed class ScalarObjectConverter : JsonConverter<object?>
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

	#endregion
}
