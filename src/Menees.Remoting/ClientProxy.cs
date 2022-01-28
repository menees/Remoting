namespace Menees.Remoting;

#region Using Directives

using System.Reflection;

#endregion

/// <summary>
/// For internal use only by <see cref="RmiClient{TServiceInterface}"/>.
/// </summary>
/// <remarks>
/// DispatchProxy.Create requires this type to be un-sealed.
/// It's also required to be public until we can reference v6.0.0 of DispatchProxy containing fix 30917.
/// As of Jan 26, 2022, v6.0.0 is still not publically available on NuGet even though the fix was supposedly
/// in 6.0.0-preview.3.21152.1 as of Mar 4, 2021 per AArnott. :-(
/// https://github.com/dotnet/runtime/issues/30917
/// <para/>
/// Note: Since this library is strongly-named, it can't used the InternalsVisibleTo("ProxyBuilder") hack.
/// https://github.com/dotnet/runtime/issues/25595#issuecomment-546330898
/// </remarks>
/// <typeparam name="TServiceInterface"></typeparam>
public class ClientProxy<TServiceInterface> : DispatchProxy
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

		object? result = this.client.Invoke(targetMethod, args ?? Array.Empty<object?>());
		return result;
	}

	#endregion
}
