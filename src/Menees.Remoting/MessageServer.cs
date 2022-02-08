﻿namespace Menees.Remoting;

#region Using Directives

using Menees.Remoting.Models;
using Menees.Remoting.Pipes;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Used to receive a <typeparamref name="TIn"/> request from a <see cref="MessageClient{TIn, TOut}"/>
/// process it, and send a <typeparamref name="TOut"/> response.
/// </summary>
/// <typeparam name="TIn">The request message type.</typeparam>
/// <typeparam name="TOut">The response message type.</typeparam>
public sealed class MessageServer<TIn, TOut> : MessageNode<TIn, TOut>, IServer
{
	#region Private Data Members

	private readonly PipeServer pipe;
	private readonly CancellationToken cancellationToken;
	private Func<TIn, CancellationToken, Task<TOut>>? requestHandler;

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
		: this(
			requestHandler != null ? (request, _) => requestHandler(request) : throw new ArgumentNullException(nameof(requestHandler)),
			serverPath,
			maxListeners,
			minListeners,
			loggerFactory)
	{
	}

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
		Func<TIn, CancellationToken, Task<TOut>> requestHandler,
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
		: this(
			requestHandler != null ? (request, _) => requestHandler(request) : throw new ArgumentNullException(nameof(requestHandler)),
			settings)
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
	public MessageServer(Func<TIn, CancellationToken, Task<TOut>> requestHandler, ServerSettings settings)
		: base(settings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException(nameof(settings));
		}

		this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));

		// Note: The pipe is created with no listeners until we explicitly start them.
		this.pipe = new(settings.ServerPath, settings.MinListeners, settings.MaxListeners, this.ProcessRequestAsync, this.Loggers);
		this.cancellationToken = settings.CancellationToken;
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
		await ServerUtility.ProcessRequestAsync(
			this,
			this,
			clientStream,
			async (request, cancellation) =>
			{
				Response response;
				Func<TIn, CancellationToken, Task<TOut>>? requestHandler = this.requestHandler;
				if (request.MethodSignature != null)
				{
					response = ServerUtility.CreateResponse(new ArgumentException("A message request should not specify a method signature."));
				}
				else if (request.Arguments?.Count != 1)
				{
					response = ServerUtility.CreateResponse(new ArgumentException("A single input message is required."));
				}
				else if (request.Arguments[0].DeserializeValue(this.UserSerializer) is not TIn inputMessage)
				{
					response = ServerUtility.CreateResponse(new ArgumentException($"The input message must be of type {typeof(TIn)}."));
				}
				else if (requestHandler == null)
				{
					response = ServerUtility.CreateResponse(new ObjectDisposedException(this.GetType().FullName));
				}
				else
				{
					TOut outputMessage = await requestHandler(inputMessage, cancellation).ConfigureAwait(false);
					response = new Response { Result = new UserSerializedValue(typeof(TOut), outputMessage, this.UserSerializer) };
				}

				return response;
			},
			this.cancellationToken).ConfigureAwait(false);
	}

	#endregion
}
