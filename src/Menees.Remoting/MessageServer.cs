namespace Menees.Remoting;

#region Using Directives

using Menees.Remoting.Pipes;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Used to receive a <typeparamref name="TIn"/> request from a <see cref="MessageClient{TIn, TOut}"/>
/// process it, and send a <typeparamref name="TOut"/> response.
/// </summary>
/// <typeparam name="TIn">The request message type.</typeparam>
/// <typeparam name="TOut">The response message type.</typeparam>
public sealed class MessageServer<TIn, TOut> : MessageNode<TIn, TOut>
{
	#region Private Data Members

	private readonly PipeServer pipe;
	private Func<TIn, Task<TOut>>? requestHandler;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new server instance to expose a <typeparamref name="TIn"/> to <typeparamref name="TOut"/>
	/// <paramref name="requestHandler"/> implementation to <see cref="MessageClient{TIn, TOut}"/> instances.
	/// </summary>
	/// <param name="requestHandler">A custom handler to process a <typeparamref name="TIn"/> request message
	/// and return a <typeparamref name="TOut"/> response message.
	/// </param>
	/// <param name="serverPath">The path used to expose the service.</param>
	/// <param name="maxListeners">The maximum number of server listener tasks to start.</param>
	/// <param name="minListeners">The minimim number of server listener tasks to start.</param>
	/// <param name="loggerFactory">An optional factory for creating type-specific server loggers for status information.</param>
	public MessageServer(
		Func<TIn, Task<TOut>> requestHandler,
		string serverPath,
		int maxListeners = ServerSettings.MaxAllowedListeners,
		int minListeners = 1,
		ILoggerFactory? loggerFactory = null)
		: this(requestHandler, new ServerSettings(serverPath)
		{
			MaxListeners = maxListeners,
			MinListeners = minListeners,
			LoggerFactory = loggerFactory,
		})
	{
	}

	/// <summary>
	/// Creates a new server instance to expose a <typeparamref name="TIn"/> to <typeparamref name="TOut"/>
	/// <paramref name="requestHandler"/> implementation to <see cref="MessageClient{TIn, TOut}"/> instances.
	/// </summary>
	/// <param name="requestHandler">A custom handler to process a <typeparamref name="TIn"/> request message
	/// and return a <typeparamref name="TOut"/> response message.
	/// </param>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	public MessageServer(Func<TIn, Task<TOut>> requestHandler, ServerSettings settings)
		: base(settings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException(nameof(settings));
		}

		this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));

		// Note: The pipe is created with no listeners until we explicitly start them.
		this.pipe = new(settings.ServerPath, settings.MinListeners, settings.MaxListeners, this.ProcessRequestAsync, this.Loggers);

		// TODO: Add support for CancellationToken server-side. [Bill, 1/30/2022]
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

	#endregion

	#region Protected Methods

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			this.requestHandler = null;
			this.pipe.Dispose();
		}
	}

	#endregion

	#region Private Methods

	private async Task ProcessRequestAsync(Stream clientStream)
	{
		// TODO: Finish ProcessRequestAsync. [Bill, 2/6/2022]
		this.GetHashCode();
		clientStream.GetHashCode();
		await Task.CompletedTask.ConfigureAwait(false);
	}

	#endregion
}
