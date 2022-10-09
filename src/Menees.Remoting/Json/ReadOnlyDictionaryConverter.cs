namespace Menees.Remoting.Json;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

/// <summary>
/// Used to convert System.Collections.ObjectModel.ReadOnlyDictionary&lt;TKey,TValue>.
/// </summary>
/// <remarks>
/// Microsoft's built-in converters handle converting IReadOnlyDictionary as a writable Dictionary
/// instance. This converter is required to deserialize a concrete ReadOnlyDictionary instance
/// since it doesn't have a default constructor. This deserializes the data into a writable
/// Dictionary instance then passes that to the ReadOnlyDictionary constructor.
/// <para/>
/// Based on https://stackoverflow.com/a/70813056/1882616, which was
/// based on https://gist.github.com/mikaeldui/1383dda4147f461ac4154406c03cc180.
/// </remarks>
internal sealed class ReadOnlyDictionaryConverter : JsonConverterFactory
{
	#region Public Methods

	public override bool CanConvert(Type typeToConvert)
	{
		// The typeToConvert must be ReadOnlyDictionary<,> or derived from it, and it must expose IReadOnlyDictionary<,>.
		bool result = typeToConvert.IsGenericType
			&& typeToConvert.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
			&& (typeToConvert.GetGenericTypeDefinition() == typeof(ReadOnlyDictionary<,>)
				|| IsSubclassOfOpenGeneric(typeof(ReadOnlyDictionary<,>), typeToConvert));
		return result;
	}

	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		Type iReadOnlyDictionary = typeToConvert.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
		Type keyType = iReadOnlyDictionary.GetGenericArguments()[0];
		Type valueType = iReadOnlyDictionary.GetGenericArguments()[1];

		JsonConverter? converter = (JsonConverter?)Activator.CreateInstance(
			typeof(ReadOnlyDictionaryConverterInner<,>).MakeGenericType(keyType, valueType),
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			args: null,
			culture: null);

		return converter;
	}

	#endregion

	#region Private Methods

	// Based on https://stackoverflow.com/a/457708/1882616.
	private static bool IsSubclassOfOpenGeneric(Type generic, Type? toCheck)
	{
		bool result = false;

		while (toCheck != null && toCheck != typeof(object))
		{
			Type current = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
			if (generic == current)
			{
				result = true;
				break;
			}

			toCheck = toCheck.BaseType;
		}

		return result;
	}

	#endregion

	#region Private Types

	private class ReadOnlyDictionaryConverterInner<TKey, TValue> : JsonConverter<IReadOnlyDictionary<TKey, TValue>>
		where TKey : notnull
	{
		public override IReadOnlyDictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			Dictionary<TKey, TValue>? dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options: options);

			IReadOnlyDictionary<TKey, TValue>? result = null;
			if (dictionary != null)
			{
				result = (IReadOnlyDictionary<TKey, TValue>?)Activator.CreateInstance(
					typeToConvert,
					BindingFlags.Instance | BindingFlags.Public,
					binder: null,
					args: new object[] { dictionary },
					culture: null);
			}

			return result;
		}

		public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<TKey, TValue> dictionary, JsonSerializerOptions options) =>
			JsonSerializer.Serialize(writer, dictionary, options);
	}

	#endregion
}
