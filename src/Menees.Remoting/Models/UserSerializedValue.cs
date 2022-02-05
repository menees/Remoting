namespace Menees.Remoting.Models;

/// <summary>
/// Used to pair a <see cref="Type"/> with a <see cref="SerializedValue"/>
/// that's been serialized using <see cref="RmiBase{TServiceInterface}.UserSerializer"/>
/// so that even null values can be serialized and deserialized with
/// <see cref="RmiBase{TServiceInterface}.SystemSerializer"/>
/// while retaining type information.
/// </summary>
internal sealed class UserSerializedValue
{
	#region Constructors

	public UserSerializedValue()
	{
		// This is required for JSON deserialization.
	}

	internal UserSerializedValue(Type dataType, object? value, ISerializer userSerializer)
	{
		// TODO: If userSerializer is null then just store value. [Bill, 2/5/2022]
		this.DataType = dataType;
		this.SerializerId = userSerializer.GetType().AssemblyQualifiedName;
		if (dataType != typeof(void))
		{
			this.SerializedValue = userSerializer.Serialize(value, dataType);
		}
	}

	#endregion

	#region Public Properties

	public Type DataType { get; set; } = typeof(object);

	// TODO: Support object? instead of byte[]? for SerializedValue. [Bill, 2/5/2022]
	public byte[]? SerializedValue { get; set; }

	public string? SerializerId { get; set; }

	#endregion

	#region Public Methods

	public object? DeserializeValue(ISerializer userSerializer)
	{
		// TODO: If userSerializer is null then just return value. [Bill, 2/5/2022]
		string? deserializerId = GetId(userSerializer);
		if (deserializerId != this.SerializerId)
		{
			throw new ArgumentException(
				$"Fully-qualified type names for serializer and deserializer do not match: S: {this.SerializerId} D:{deserializerId}");
		}

		object? result = this.SerializedValue != null ? userSerializer.Deserialize(this.SerializedValue, this.DataType) : null;
		return result;
	}

	#endregion

	#region Private Methods

	private static string? GetId(ISerializer userSerializer)
		=> userSerializer.GetType().AssemblyQualifiedName;

	#endregion
}
