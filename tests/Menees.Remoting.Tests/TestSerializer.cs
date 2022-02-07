namespace Menees.Remoting;

using System;
using System.Text.Json;

internal sealed class TestSerializer : ISerializer
{
	// Use an atypical encoding that uses 2 bytes per character.
	private static readonly Encoding TestEncoding = Encoding.BigEndianUnicode;

	public object? Deserialize(byte[] serializedValue, Type returnType)
	{
		string json = TestEncoding.GetString(serializedValue);
		object? result = JsonSerializer.Deserialize(json, returnType);
		return result;
	}

	public byte[] Serialize(object? value, Type valueType)
	{
		string json = JsonSerializer.Serialize(value, valueType);
		byte[] result = TestEncoding.GetBytes(json);
		return result;
	}
}
