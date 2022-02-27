namespace Menees.Remoting;

#region Using Directives

using System.Reflection;
using Menees.Remoting.Models;
using Menees.Remoting.Pipes;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Exposes the <typeparamref name="TServiceInterface"/> interface from a given service object instance
/// as a remotely invokable server.
/// </summary>
/// <typeparam name="TServiceInterface">The interface to make available for remote invocation.</typeparam>
public sealed class RmiServer<TServiceInterface> : RmiNode<TServiceInterface>, IServer
	where TServiceInterface : class
{
	#region Private Data Members

	private static readonly Dictionary<string, MethodInfo> MethodSignatureCache =
		typeof(TServiceInterface).GetMethods().ToDictionary(method => GetMethodSignature(method));

	private readonly PipeServer pipe;
	private readonly CancellationToken cancellationToken;

	private TServiceInterface? serviceInstance;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new server instance to expose a <typeparamref name="TServiceInterface"/> implementation
	/// to <see cref="RmiClient{TServiceInterface}"/> instances.
	/// </summary>
	/// <param name="serviceInstance">An instance of <typeparamref name="TServiceInterface"/> on which to execute remote invocations.</param>
	/// <param name="serverPath">The path used to expose the service.</param>
	/// <param name="maxListeners">The maximum number of server listener tasks to start.</param>
	/// <param name="minListeners">The minimim number of server listener tasks to start.</param>
	/// <param name="loggerFactory">An optional factory for creating type-specific server loggers for status information.</param>
	public RmiServer(
		TServiceInterface serviceInstance,
		string serverPath,
		int maxListeners = ServerSettings.MaxAllowedListeners,
		int minListeners = 1,
		ILoggerFactory? loggerFactory = null)
		: this(serviceInstance, new ServerSettings(serverPath)
		{
			MaxListeners = maxListeners,
			MinListeners = minListeners,
			LoggerFactory = loggerFactory,
		})
	{
	}

	/// <summary>
	/// Creates a new server instance to expose a <typeparamref name="TServiceInterface"/> implementation
	/// to <see cref="RmiClient{TServiceInterface}"/> instances.
	/// </summary>
	/// <param name="serviceInstance">An instance of <typeparamref name="TServiceInterface"/> on which to execute remote invocations.</param>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	public RmiServer(TServiceInterface serviceInstance, ServerSettings settings)
		: base(settings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException(nameof(settings));
		}

		this.serviceInstance = serviceInstance ?? throw new ArgumentNullException(nameof(serviceInstance));

		// Note: The pipe is created with no listeners until we explicitly start them.
		this.pipe = new(
			settings.ServerPath,
			settings.MinListeners,
			settings.MaxListeners,
			this.ProcessRequestAsync,
			this,
			this.Loggers,
			(PipeServerSecurity?)settings.Security);
		this.cancellationToken = settings.CancellationToken;
	}

	#endregion

	#region Public Events

	/// <inheritdoc/>
	public event EventHandler? Stopped
	{
		add => this.pipe.Stopped += value;
		remove => this.pipe.Stopped -= value;
	}

	#endregion

	#region Public Properties

	/// <inheritdoc/>
	public Action<Exception>? ReportUnhandledException
	{
		get => this.pipe.ReportUnhandledException;
		set => this.pipe.ReportUnhandledException = value;
	}

	#endregion

	#region Public Methods

	/// <inheritdoc/>
	public void Start() => this.pipe.EnsureMinListeners();

	/// <inheritdoc/>
	public void Stop() => this.pipe.StopListening();

	#endregion

	#region Protected Methods

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			this.serviceInstance = null;
			this.pipe.Dispose();
		}
	}

	#endregion

	#region Private Methods

	private async Task ProcessRequestAsync(Stream clientStream)
	{
		await ServerUtility.ProcessRequestAsync(
			this,
			this,
			clientStream,
			async (request, cancellation) =>
			{
				Response response;

				if (!MethodSignatureCache.TryGetValue(request.MethodSignature ?? string.Empty, out MethodInfo? method))
				{
					response = ServerUtility.CreateResponse(new TargetException(
						$"A {typeof(TServiceInterface).FullName} method with signature '{request.MethodSignature}' was not found."));
				}
				else if (this.serviceInstance is not object target)
				{
					response = ServerUtility.CreateResponse(new ObjectDisposedException(this.GetType().FullName));
				}
				else
				{
					IEnumerable<UserSerializedValue> serializedArgs = request.Arguments ?? Enumerable.Empty<UserSerializedValue>();
					object?[] args = serializedArgs.Select(arg => arg.DeserializeValue(this.UserSerializer)).ToArray();
					object? methodResult;
					try
					{
						methodResult = method.Invoke(target, args);
						Type returnType = methodResult?.GetType() ?? method.ReturnType;

						// In theory, if the return type is Task then we could await methodResult. However, that gets complicated
						// very quickly since the client is synchronously waiting for the result. We can't serialize Task or CancellationToken
						// directly, so we'd have to implement custom support for them in the client and server. For now, we'll keep things
						// simple and let the serializer throw if those types are used. A caller can use MessageServer for async calls instead.
						response = new Response { Result = new UserSerializedValue(returnType, methodResult, this.UserSerializer) };
					}
					catch (TargetInvocationException ex)
					{
						// The inner exception is the original exception thrown by the invoked method.
						response = ServerUtility.CreateResponse(ex.InnerException ?? ex);
						methodResult = null;
					}

					// The inner try..catch for Invoke only handles TargetInvocationException so any exceptions from
					// invalid request arguments or from trying to serialize methodResult will pass through the
					// ReportUnhandledException action below before being returned.
				}

				return await Task.FromResult(response).ConfigureAwait(false);
			},
			this.cancellationToken).ConfigureAwait(false);
	}

	#endregion
}
