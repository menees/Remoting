namespace Menees.Remoting.Json;

#region Using Directives

using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

/// <summary>
/// Send System.Type as an assembly-qualified type name.
/// </summary>
internal sealed class SystemTypeConverter : JsonConverter<Type>
{
	#region Private Data Members

	private readonly Func<string, Type?> tryGetType;

	#endregion

	#region Constructors

	public SystemTypeConverter(Func<string, Type?> tryGetType)
	{
		this.tryGetType = tryGetType;
	}

	#endregion

	#region Public Methods

	public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? assemblyQualifiedName = reader.GetString();
		Type? result = assemblyQualifiedName != null ? this.tryGetType(assemblyQualifiedName) : null;
		return result;
	}

	public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.AssemblyQualifiedName);
	}

	#endregion
}
