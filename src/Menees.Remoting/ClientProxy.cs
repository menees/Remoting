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
/// <para/>
/// It's also required to be public for .NET Framework since we can't reference v6.0.0 of DispatchProxy containing fix 30917.
/// https://github.com/dotnet/runtime/issues/30917
/// https://github.com/dotnet/runtime/discussions/64726#discussioncomment-2113733
/// <para/>
/// Note: Since this library is strongly-named, it can't use the InternalsVisibleTo("ProxyBuilder") hack.
/// https://github.com/dotnet/runtime/issues/25595#issuecomment-546330898
/// </remarks>
/// <typeparam name="TServiceInterface"></typeparam>
#if NETFRAMEWORK
[EditorBrowsable(EditorBrowsableState.Never)]
public
#else
internal
#endif
class ClientProxy<TServiceInterface> : DispatchProxy
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

		if (targetMethod == null)
		{
			throw new ArgumentNullException(nameof(targetMethod));
		}

		// This requires a synchronous call from the client to avoid deadlocks since DispatchProxy.Invoke is synchronous.
		object? result = this.client.Invoke(targetMethod, args ?? Array.Empty<object?>());
		return result;
	}

	#endregion
}
