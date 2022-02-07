namespace Menees.Remoting.Json;

#region Using Directives

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Menees.Remoting.Models;

#endregion

internal sealed class UserSerializedValueConverter : JsonConverter<UserSerializedValue>
{
	#region Public Methods

	public override UserSerializedValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.StartObject
			&& reader.Read()
			&& reader.TokenType == JsonTokenType.PropertyName
			&& reader.GetString() == nameof(UserSerializedValue.SerializerId)
			&& reader.Read()
			&& (reader.TokenType == JsonTokenType.String
				|| reader.TokenType == JsonTokenType.Null))
		{
			UserSerializedValue result = new() { SerializerId = reader.GetString() };

			if (reader.Read()
				&& reader.TokenType == JsonTokenType.PropertyName
				&& reader.GetString() == nameof(UserSerializedValue.DataType))
			{
				Type? dataType = (Type?)JsonSerializer.Deserialize(ref reader, typeof(Type), options);
				if (dataType != null)
				{
					result.DataType = dataType;
					if (reader.Read()
						&& reader.TokenType == JsonTokenType.PropertyName
						&& reader.GetString() == nameof(UserSerializedValue.SerializedValue)
						&& reader.Read())
					{
						// If SerializerId is null, then only the system JSerializer was used, so we can apply dataType.
						// If SerializerId is non-null, then a user serializer was used, and it needs a byte[]. The user
						// serializer will deserialize the byte[] to the dataType later.
						Type valueType = result.SerializerId == null ? dataType : typeof(byte[]);
						if (valueType != typeof(void))
						{
							result.SerializedValue = JsonSerializer.Deserialize(ref reader, valueType, options);
						}

						if (reader.Read()
							&& reader.TokenType == JsonTokenType.EndObject)
						{
							return result;
						}
					}
				}
			}
		}

		throw new JsonException($"Invalid {nameof(UserSerializedValue)} JSON");
	}

	public override void Write(Utf8JsonWriter writer, UserSerializedValue value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		writer.WriteString(nameof(value.SerializerId), value.SerializerId);

		// This will use SystemTypeConverter to write the Type.
		writer.WritePropertyName(nameof(value.DataType));
		JsonSerializer.Serialize(writer, value.DataType, typeof(Type), options);

		writer.WritePropertyName(nameof(value.SerializedValue));
		if (value.DataType == typeof(void))
		{
			writer.WriteNullValue();
		}
		else
		{
			Type valueType = value.SerializerId == null ? value.DataType : typeof(byte[]);
			JsonSerializer.Serialize(writer, value.SerializedValue, valueType, options);
		}

		writer.WriteEndObject();
	}

	#endregion
}
