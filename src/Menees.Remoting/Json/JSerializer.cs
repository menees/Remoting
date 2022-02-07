namespace Menees.Remoting.Json;

#region Using Directives

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

#endregion

internal sealed class JSerializer : ISerializer
{
	#region Private Data Members

	private static readonly Encoding SerilizerEncoding = Encoding.UTF8;

	private static readonly JsonSerializerOptions SharedSerializerOptions = new()
	{
		Converters =
		{
			// Serialize TimeSpan using its invariant "c" format (i.e., ToString()) instead of as a multi-property struct.
			JsonMetadataServices.TimeSpanConverter,

			// Deserialize scalar Object values by inferring a preferred type instead of always returning JsonElement.
			new ScalarObjectConverter(),

			// Handle user vs. system serialized values.
			new UserSerializedValueConverter(),
		},

		// Make sure ValueTuple serializes correctly since it uses public fields.
		// https://stackoverflow.com/a/58139922/1882616
		// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonTypeInfo.cs#L253
		IncludeFields = true,
	};

	private readonly JsonSerializerOptions options;

	#endregion

	#region Constructors

	public JSerializer(SystemTypeConverter systemTypeConverter)
	{
		this.options = new(SharedSerializerOptions);
		this.options.Converters.Add(systemTypeConverter);
	}

	#endregion

	#region Public Methods

	public object? Deserialize(byte[] serializedValue, Type returnType)
	{
		string json = SerilizerEncoding.GetString(serializedValue);
		object? result = JsonSerializer.Deserialize(json, returnType, this.options);
		return result;
	}

	public byte[] Serialize(object? value, Type valueType)
	{
		string json = JsonSerializer.Serialize(value, valueType, this.options);
		byte[] result = SerilizerEncoding.GetBytes(json);
		return result;
	}

#endregion
}
