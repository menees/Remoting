namespace Menees.Remoting
{
	/// <summary>
	/// Used to pair a <see cref="Type"/> with a <see cref="Value"/>
	/// so that even null values can be serialized and deserialized
	/// while retaining type information.
	/// </summary>
	internal sealed class TypedValue
	{
		public Type Type { get; set; } = typeof(object);

		public object? Value { get; set; }
	}
}
