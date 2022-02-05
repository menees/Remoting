namespace Menees.Remoting;

#region Using Directives

using System.Reflection;
using Menees.Remoting.Models;
using Menees.Remoting.Pipes;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Used to invoke a <typeparamref name="TServiceInterface"/> interface member on a <see cref="RmiServer{T}"/>.
/// </summary>
/// <typeparam name="TServiceInterface">The interface to remotely invoke members on.</typeparam>
public sealed class RmiClient<TServiceInterface> : RmiBase<TServiceInterface>
	where TServiceInterface : class
{
	#region Private Data Members

	private readonly PipeClient pipe;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new client instance to invoke methods on a <see cref="RmiServer{TServiceInterface}"/> instance.
	/// </summary>
	/// <param name="serverPath">The path used to expose the service.</param>
	/// <param name="connectTimeout">The interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// If null, then <see cref="ClientSettings.DefaultConnectTimeout"/> is used.
	/// </param>
	/// <param name="loggerFactory">An optional factory for creating type-specific server loggers for status information.</param>
	public RmiClient(
		string serverPath,
		TimeSpan? connectTimeout = null,
		ILoggerFactory? loggerFactory = null)
		: this(new ClientSettings(serverPath)
		{
			ConnectTimeout = connectTimeout ?? ClientSettings.DefaultConnectTimeout,
			LoggerFactory = loggerFactory,
		})
	{
	}

	/// <summary>
	/// Creates a new client instance to invoke methods on a <see cref="RmiServer{TServiceInterface}"/> instance.
	/// </summary>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	public RmiClient(ClientSettings settings)
		: base(settings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException(nameof(settings));
		}

		this.ConnectTimeout = settings.ConnectTimeout;
		this.pipe = new(settings.ServerPath, settings.ServerHost, this.Loggers);
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets the interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// </summary>
	public TimeSpan ConnectTimeout { get; }

	#endregion

	#region Public Methods

	/// <summary>
	/// Creates a <typeparamref name="TServiceInterface"/> proxy that remotely
	/// invokes members on an <see cref="RmiServer{TServiceInterface}"/> using the
	/// path passed to this <see cref="RmiClient{TServiceInterface}"/>'s constructor.
	/// </summary>
	/// <returns>A new proxy instance associated with this client.</returns>
	public TServiceInterface CreateProxy()
	{
		TServiceInterface result = DispatchProxy.Create<TServiceInterface, ClientProxy<TServiceInterface>>();
		if (result is not ClientProxy<TServiceInterface> proxy)
		{
			throw new InvalidOperationException("Unsupported proxy type.");
		}

		proxy.Initialize(this);
		return result;
	}

	#endregion

	#region Internal Methods

	internal object? Invoke(MethodInfo targetMethod, object?[] args)
	{
		Request request = this.CreateRequest(targetMethod, args);

		Response? response = null;
		this.pipe.SendRequest(this.ConnectTimeout, stream =>
		{
			request.WriteTo(stream, this.SystemSerializer);
			response = Message.ReadFrom<Response>(stream, this.SystemSerializer);
		});

		response?.Error?.ThrowException();
		object? result = response?.Result?.DeserializeValue(this.UserSerializer);
		return result;
	}

	#endregion

	#region Protected Methods

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			this.pipe.Dispose();
		}
	}

	#endregion

	#region Private Methods

	private Request CreateRequest(MethodInfo targetMethod, object?[] args)
	{
		int argCount = args.Length;
		List<UserSerializedValue> arguments = new(argCount);
		ParameterInfo[]? parameters = null;

		for (int i = 0; i < argCount; i++)
		{
			object? value = args[i];
			Type? dataType = value?.GetType();
			if (dataType == null)
			{
				parameters ??= targetMethod.GetParameters();
				if (i < parameters.Length)
				{
					dataType = parameters[i].ParameterType;
				}
			}

			dataType ??= typeof(object);
			arguments.Add(new UserSerializedValue(dataType, value, this.UserSerializer));
		}

		Request request = new()
		{
			MethodSignature = GetMethodSignature(targetMethod),
			Arguments = arguments,
		};

		return request;
	}

	#endregion
}
