namespace Menees.Remoting;

#region Using Directives

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Menees.Remoting.Json;

#endregion

/// <summary>
/// Shared functionality for <see cref="RmiClient{T}"/> and <see cref="RmiServer{T}"/>.
/// </summary>
/// <typeparam name="TServiceInterface">The interface to remotely invoke/expose members for.</typeparam>
public abstract class RmiNode<TServiceInterface> : Node
	where TServiceInterface : class
{
	#region Constructors

	/// <summary>
	/// Validates that <typeparamref name="TServiceInterface"/> is an interface.
	/// </summary>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	protected RmiNode(NodeSettings settings)
		: base(settings)
	{
		Type interfaceType = typeof(TServiceInterface);
		if (!interfaceType.IsInterface)
		{
			throw new ArgumentException($"{nameof(TServiceInterface)} {interfaceType.FullName} must be an interface type.");
		}
	}

	#endregion

	#region Private Protected Methods

	private protected static string GetMethodSignature(MethodInfo methodInfo)
	{
		// For our purposes MethodInfo.ToString() returns a unique enough signature.
		// For example, typeof(string).GetMethods().Last(m => m.Name == "IndexOf").ToString()
		// returns "Int32 IndexOf(Char, Int32, Int32)".
		// For other options see: https://stackoverflow.com/a/1312321/1882616
		string result = methodInfo.ToString() ?? throw new InvalidOperationException("Null method signature is not supported.");
		return result;
	}

	#endregion
}
