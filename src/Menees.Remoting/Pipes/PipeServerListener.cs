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

	private bool disposed;

	#endregion

	#region Constructors

	public PipeServerListener(PipeServer server, NamedPipeServerStream pipe)
	{
		this.server = server;
		this.pipe = pipe;
		this.State = ListenerState.Created;
	}

	#endregion

	#region Public Properties

	public ListenerState State { get; private set; }

	#endregion

	#region Public Methods

	public void Dispose()
	{
		if (!this.disposed)
		{
			this.disposed = true;
			this.State = ListenerState.Disposed;

			if (this.pipe.IsConnected)
			{
				this.server.LogTrace("Disconnecting listener.");
				try
				{
					this.pipe.Disconnect();
				}
				catch (Exception ex)
				{
					this.server.Log(LogLevel.Error, ex, "Exception disconnecting listener.");
				}
			}

			this.server.LogTrace("Disposing listener.");
			try
			{
				this.pipe.Dispose();
			}
			catch (Exception ex)
			{
				this.server.Log(LogLevel.Error, ex, "Exception disposing listener.");
			}
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
			this.server.Log(level, ex, "Wait for pipe connection failed.");
		}
		catch (ObjectDisposedException ex)
		{
			this.server.Log(LogLevel.Debug, ex, "Listener disposed while waiting for pipe.");
		}
		catch (Exception ex)
		{
			this.server.Log(LogLevel.Error, ex, "Unhandled exception waiting for pipe connection.");
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
				this.server.LogTrace("Listener connected");
				this.server.EnsureMinListeners();

				this.State = ListenerState.ProcessingRequest;
				try
				{
					await this.server.ProcessRequestAsync(this.pipe).ConfigureAwait(false);
					this.State = ListenerState.FinishedRequest;
				}
				catch (Exception ex)
				{
					this.server.Log(LogLevel.Error, ex, "Error processing request.");
					this.server.ReportUnhandledException?.Invoke(ex);
				}
			}

			this.server.LogTrace("Stopping listener.");

			// Self dispose since each listener should only be used for a single request.
			this.Dispose();

			// Poke the server to indicate it should start another listener if necessary.
			// If it was at its max earlier when we started processing, then maybe now
			// that we're finished it'll be below the max (unless another thread snuck in
			// and started a new listener).
			this.server.LogTrace($"After listener dispose");
			this.server.EnsureMinListeners();
		}
	}

	#endregion
}
