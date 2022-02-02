namespace Menees.Remoting;

#region Using Directives

using System.Reflection;

#endregion

/// <summary>
/// For internal use only by <see cref="RmiClient{TServiceInterface}"/>.
/// </summary>
/// <remarks>
/// DispatchProxy.Create requires this type to be un-sealed.
/// <para/>
/// It's also required to be public for .NET Framework until we can reference v6.0.0 of DispatchProxy containing
/// fix 30917. As of Jan 26, 2022, v6.0.0 is still not publically available on NuGet even though the fix was in
/// 6.0.0-preview.3.21152.1 as of Mar 4, 2021 per AArnott. The v6 libraray is available to .NET 6 builds via
/// the SDK, but it's not available as a NuGet package for a .NET Framework target. :-(
/// https://github.com/dotnet/runtime/issues/30917
/// <para/>
/// Note: Since this library is strongly-named, it can't used the InternalsVisibleTo("ProxyBuilder") hack.
/// https://github.com/dotnet/runtime/issues/25595#issuecomment-546330898
/// </remarks>
/// <typeparam name="TServiceInterface"></typeparam>
#if NETFRAMEWORK
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
		// TODO: Log issue to get v6.0.0 of DispatchProxy published. [Bill, 1/30/2022]
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
	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
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
