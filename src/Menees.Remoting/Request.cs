namespace Menees.Remoting
{
	using System;
	using System.Collections.Generic;

	internal sealed class Request : Message
	{
		public string? MethodSignature { get; set; }

		public List<(object? Value, Type DataType)>? Arguments { get; set; }
	}
}
