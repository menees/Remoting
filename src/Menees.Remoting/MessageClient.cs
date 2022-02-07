namespace Menees.Remoting;

#region Using Directives

using Menees.Remoting.Models;
using Menees.Remoting.Pipes;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Used to send a <typeparamref name="TIn"/> request to a <see cref="MessageServer{TIn, TOut}"/>
/// and receive a <typeparamref name="TOut"/> response.
/// </summary>
/// <typeparam name="TIn">The request message type.</typeparam>
/// <typeparam name="TOut">The response message type.</typeparam>
public sealed class MessageClient<TIn, TOut> : MessageNode<TIn, TOut>
{
	#region Private Data Members

	private readonly PipeClient pipe;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new client instance to invoke methods on a <see cref="MessageServer{TIn, TOut}"/> instance.
	/// </summary>
	/// <param name="serverPath">The path used to expose the service.</param>
	/// <param name="connectTimeout">The interval to wait for a connection to a remote <see cref="MessageServer{TIn, TOut}"/>.
	/// If null, then <see cref="ClientSettings.DefaultConnectTimeout"/> is used.
	/// </param>
	/// <param name="loggerFactory">An optional factory for creating type-specific server loggers for status information.</param>
	public MessageClient(
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
	/// Creates a new client instance to invoke methods on a <see cref="MessageServer{TIn, TOut}"/> instance.
	/// </summary>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	public MessageClient(ClientSettings settings)
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
	/// Sends a <typeparamref name="TIn"/> request to a <see cref="MessageServer{TIn, TOut}"/>
	/// and returns the <typeparamref name="TOut"/> response.
	/// </summary>
	/// <param name="message">The request message to send.</param>
	/// <returns>The response message recevied from the server.</returns>
	public async Task<TOut> SendAsync(TIn message)
	{
		Request request = new()
		{
			Arguments = new() { new UserSerializedValue(typeof(TIn), message, this.UserSerializer) },
		};

		Response? response = null;
		await this.pipe.SendRequestAsync(this.ConnectTimeout, async stream =>
		{
			await request.WriteToAsync(stream, this.SystemSerializer).ConfigureAwait(false);
			response = await Message.ReadFromAsync<Response>(stream, this.SystemSerializer).ConfigureAwait(false);
		}).ConfigureAwait(false);

		response?.Error?.ThrowException();
		object? rawResult = response?.Result?.DeserializeValue(this.UserSerializer);
		TOut result = (TOut)rawResult!;
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
}
