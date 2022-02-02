namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeServerListener : IDisposable
{
	#region Private Data Members

	private readonly PipeServer server;
	private readonly NamedPipeServerStream pipe;
	private readonly ILogger logger;

	private IDisposable? logScope;
	private bool disposed;

	#endregion

	#region Constructors

	public PipeServerListener(PipeServer server, NamedPipeServerStream pipe, ILogger logger)
	{
		this.server = server;
		this.pipe = pipe;
		this.logger = logger;
		this.logScope = this.logger.BeginScope(server.CreateScope());
		this.State = ListenerState.Created;
	}

	#endregion

	#region Public Properties

	public ListenerState State { get; private set; }

	#endregion

	#region Public Methods

	public void Dispose()
	{
		// Note: This method can be called multiple times if a listener is finishing and self-disposes on one thread,
		// and the server finishes and disposes its remaining listeners from another thread.
		if (!this.disposed)
		{
			this.disposed = true;
			this.State = ListenerState.Disposed;

			if (this.pipe.IsConnected)
			{
				this.logger.LogTrace("Disconnecting listener.");
				try
				{
					this.pipe.Disconnect();
				}
				catch (Exception ex)
				{
					this.logger.LogError(ex, "Exception disconnecting listener.");
				}
			}

			this.logger.LogTrace("Disposing listener.");
			try
			{
				this.pipe.Dispose();
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Exception disposing listener.");
			}

			this.logScope?.Dispose();
			this.logScope = null;
		}
	}

	public async Task StartAsync()
	{
		this.State = ListenerState.WaitingForConnection;
		try
		{
			// Stephen Toub says WaitForConnectionAsync will throw an exception (correctly)
			// when the stream is closed on Windows and on Unix.
			// https://github.com/dotnet/runtime/issues/24007#issuecomment-340810385
			await this.pipe.WaitForConnectionAsync().ConfigureAwait(false);
			this.State = ListenerState.Connected;
		}
		catch (IOException ex)
		{
			// We can get "The pipe has been ended." if the client closed early.
			LogLevel level = this.disposed ? LogLevel.Debug : LogLevel.Error;
			this.logger.Log(level, ex, "Wait for pipe connection failed."); // Note: this.logScope may be disposed already.
		}
		catch (ObjectDisposedException ex)
		{
			// We'll get "Cannot access a closed pipe." under normal conditions when the server is disposed.
			LogLevel level = this.disposed ? LogLevel.Trace : LogLevel.Debug;
			this.logger.Log(level, ex, "Listener disposed while waiting for pipe."); // Note: this.logScope may be disposed already.
		}
		catch (Exception ex)
		{
			this.logger.Log(LogLevel.Error, ex, "Unhandled exception waiting for pipe connection."); // Note: this.logScope may be disposed already.
			this.server.ReportUnhandledException?.Invoke(ex);
		}

		// Per NamedPipeServerInstance at https://www.codeproject.com/Articles/1199046/A-Csharp-Named-Pipe-Library-That-Supports-Multiple,
		// the wait will end if the listener is disposed while it's still waiting for a connection. In that case, we should do nothing.
		if (!this.disposed)
		{
			if (this.State == ListenerState.Connected)
			{
				// Since this listener is now connected (and about to begin processing a request), tell the server so it
				// can start another listener if necessary. If the server is already at its max, it may not be able to.
				this.logger.LogTrace("Listener connected");
				this.server.EnsureMinListeners();

				this.State = ListenerState.ProcessingRequest;
				try
				{
					await this.server.ProcessRequestAsync(this.pipe).ConfigureAwait(false);
					this.State = ListenerState.FinishedRequest;
				}
				catch (Exception ex)
				{
					this.logger.LogError(ex, "Error processing request.");
					this.server.ReportUnhandledException?.Invoke(ex);
				}
			}

			this.logger.LogTrace("Stopping listener.");

			// Self dispose since each listener should only be used for a single request.
			this.Dispose();

			// Poke the server to indicate it should start another listener if necessary.
			// If it was at its max earlier when we started processing, then maybe now
			// that we're finished it'll be below the max (unless another thread snuck in
			// and started a new listener).
			this.logger.LogTrace($"After listener dispose");
			this.server.EnsureMinListeners();
		}
	}

	#endregion
}
