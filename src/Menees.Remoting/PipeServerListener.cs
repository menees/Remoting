namespace Menees.Remoting;

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
		await this.pipe.WaitForConnectionAsync().ConfigureAwait(false);

		// Per NamedPipeServerInstance at https://www.codeproject.com/Articles/1199046/A-Csharp-Named-Pipe-Library-That-Supports-Multiple,
		// the wait will end if the listener is disposed while it's still waiting for a connection. In that case, we should do nothing.
		if (!this.disposed)
		{
			this.State = ListenerState.Connected;

			// Since this listener is now connected (and about to begin processing a request), tell the server so it
			// can start another listener if necessary. If the server is already at its max, it may not be able to.
			this.server.LogTrace("Listener connected");
			this.server.EnsureMinListeners();

			this.State = ListenerState.ProcessingRequest;
			try
			{
				await this.server.ProcessRequestAsync(this.pipe).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				this.server.ReportUnhandledException?.Invoke(ex);
			}

			this.server.LogTrace("Stopping listener.");

			this.State = ListenerState.FinishedRequest;

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
