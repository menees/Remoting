namespace Menees.Remoting
{
	using System;

	/// <summary>
	/// Defines the interface for converting .NET objects to and from byte arrays
	/// for transmission between <see cref="RmiClient{TServiceInterface}"/> and
	/// <see cref="RmiServer{TServiceInterface}"/>.
	/// </summary>
	public interface ISerializer
	{
		/// <summary>
		/// Converts a byte array into a .NET object.
		/// </summary>
		/// <param name="serializedValue">A previously serialized value as a byte array.</param>
		/// <param name="returnType">The .NET type to deserialize into.</param>
		/// <returns>A .NET object instance of type <paramref name="returnType"/> or null.</returns>
		object? Deserialize(byte[] serializedValue, Type returnType);

		/// <summary>
		/// Converts a .NET object into a byte array.
		/// </summary>
		/// <param name="value">The value to serialize into bytes.</param>
		/// <param name="valueType">The .NET type of <paramref name="value"/>.</param>
		/// <returns>A new byte array with the serialized form of <paramref name="value"/>.</returns>
		byte[] Serialize(object? value, Type valueType);
	}
}
