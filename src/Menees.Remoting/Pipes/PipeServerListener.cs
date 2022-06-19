namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeServerListener : PipeNode
{
	#region Private Data Members

	private readonly PipeServer server;
	private readonly NamedPipeServerStream pipe;

	private bool disposed;

	#endregion

	#region Constructors

	public PipeServerListener(PipeServer server, NamedPipeServerStream pipe, Node owner)
		: base(server.PipeName, owner)
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
			this.Logger.Log(level, ex, "Wait for pipe connection failed."); // Note: this.logScope may be disposed already.
		}
		catch (ObjectDisposedException ex)
		{
			// We'll get "Cannot access a closed pipe." under normal conditions when the server is disposed.
			LogLevel level = this.disposed ? LogLevel.Trace : LogLevel.Debug;
			this.Logger.Log(level, ex, "Listener disposed while waiting for pipe."); // Note: this.logScope may be disposed already.
		}
		catch (Exception ex)
		{
			this.Logger.Log(LogLevel.Error, ex, "Unhandled exception waiting for pipe connection."); // Note: this.logScope may be disposed already.
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
				this.Logger.LogTrace("Listener connected");
				this.server.EnsureMinListeners();

				this.State = ListenerState.ProcessingRequest;
				try
				{
					// I considered supporting a PipeServerSecurity constructor with a RunAsClient bool property.
					// If RunAsClient was true, then this would use NamedPipeServerStream.RunAsClient to invoke
					// the server's ProcessRequestAsync function while impersonating the client. Unfortunately,
					// the NamedPipeServerStream.RunAsClient method only supports a synchronous Action. Calling
					// that would be "sync over async", which is bad due to deadlock risks. So RunAsClient is out.
					// https://devblogs.microsoft.com/pfxteam/should-i-expose-synchronous-wrappers-for-asynchronous-methods/
					await this.server.ProcessRequestAsync(this.pipe).ConfigureAwait(false);
					this.State = ListenerState.FinishedRequest;
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Error processing request.");
					this.server.ReportUnhandledException?.Invoke(ex);
				}
			}

			this.Logger.LogTrace("Stopping listener.");

			// Self dispose since each listener should only be used for a single request.
			this.Dispose();

			// Poke the server to indicate it should start another listener if necessary.
			// If it was at its max earlier when we started processing, then maybe now
			// that we're finished it'll be below the max (unless another thread snuck in
			// and started a new listener).
			this.Logger.LogTrace($"After listener dispose");
			this.server.EnsureMinListeners();
		}
	}

	#endregion

	#region Protected Methods

	protected override void Dispose(bool disposing)
	{
		// Note: This method can be called multiple times if a listener is finishing and self-disposes on one thread,
		// and the server finishes and disposes its remaining listeners from another thread.
		base.Dispose(disposing);
		if (disposing && !this.disposed)
		{
			this.disposed = true;
			this.State = ListenerState.Disposed;

			if (this.pipe.IsConnected)
			{
				this.Logger.LogTrace("Disconnecting listener.");
				try
				{
					this.pipe.Disconnect();
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Exception disconnecting listener.");
				}
			}

			this.Logger.LogTrace("Disposing listener.");
			try
			{
				this.pipe.Dispose();
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Exception disposing listener.");
			}
		}
	}

	#endregion
}
