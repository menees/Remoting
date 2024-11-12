namespace Menees.Remoting;

#region Using Directives

using System.ComponentModel;
using System.Reflection;

#endregion

/// <summary>
/// For internal use only by <see cref="RmiClient{TServiceInterface}"/>.
/// </summary>
/// <remarks>
/// DispatchProxy.Create requires this type to be un-sealed.
/// </remarks>
/// <typeparam name="TServiceInterface"></typeparam>
internal class ClientProxy<TServiceInterface> : DispatchProxy
	where TServiceInterface : class
{
	#region Private Data Members

	private RmiClient<TServiceInterface>? client;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new, uninitialized instance (i.e., it's not attached to an <see cref="RmiClient{TServiceInterface}"/> instance).
	/// </summary>
	public ClientProxy()
	{
		// Note: DispatchProxy.Create requires a public default constructor for this.
	}

	#endregion

	#region Internal Methods

	internal void Initialize(RmiClient<TServiceInterface> client)
	{
		this.client = client;
	}

	#endregion

	#region Protected Methods

	/// <summary>
	/// Invokes a method through an associated <see cref="RmiClient{TServiceInterface}"/> instance.
	/// </summary>
	/// <param name="targetMethod"></param>
	/// <param name="args"></param>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	/// <exception cref="ArgumentNullException"></exception>
	protected sealed override object? Invoke(MethodInfo? targetMethod, object?[]? args)
	{
		if (this.client == null)
		{
			throw new InvalidOperationException("Client proxy was not initialized.");
		}

		ArgumentNullException.ThrowIfNull(targetMethod);

		// This requires a synchronous call from the client to avoid deadlocks since DispatchProxy.Invoke is synchronous.
		object? result = this.client.Invoke(targetMethod, args ?? []);
		return result;
	}

	#endregion
}
