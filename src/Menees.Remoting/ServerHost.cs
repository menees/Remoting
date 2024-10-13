namespace Menees.Remoting;

#region Using Directives

using System.ComponentModel;
using System.Runtime.CompilerServices;

#endregion

/// <summary>
/// A basic host for <see cref="IServer"/> instances that can coordinate an <see cref="IServerHost.Exit"/> request.
/// </summary>
public sealed class ServerHost : IServerHost, IDisposable
{
	#region Private Data Members

	private readonly ManualResetEventSlim resetEvent = new(false);
	private readonly HashSet<IServer> servers = [];
	private bool isDisposed;
	private bool isExiting;

	#endregion

	#region Public Events

	/// <summary>
	/// Raised when <see cref="IServerHost.Exit(int?)"/> is called to allow logging and/or cancellation.
	/// </summary>
	public event CancelEventHandler? Exiting;

	#endregion

	#region Public Properties

	bool IServerHost.IsReady => !this.resetEvent.IsSet;

	/// <summary>
	/// Gets the exit code passed to <see cref="IServerHost.Exit"/>.
	/// </summary>
	public int? ExitCode { get; private set; }

	#endregion

	#region Public Methods

	/// <inheritdoc/>
	/// <exception cref="ObjectDisposedException">If <see cref="Dispose()"/> has been called already.</exception>
	/// <exception cref="InvalidOperationException">If <see cref="IServerHost.Exit"/> has started already.</exception>
	bool IServerHost.Exit(int? exitCode)
	{
		this.EnsureReady();

		CancelEventArgs should = new();
		int? previousExitCode = this.ExitCode;
		try
		{
			this.ExitCode = exitCode;
			this.Exiting?.Invoke(this, should);
		}
		finally
		{
			if (should.Cancel)
			{
				this.ExitCode = previousExitCode;
			}
		}

		bool exit = !should.Cancel;
		if (exit)
		{
			// Give the caller a little time to receive our response and disconnect.
			// Otherwise, this process could end too soon, and the client would get an ArgumentException
			// like "Unable to read 4 byte message length from stream. Only 0 bytes were available.".
			this.StartExiting();
		}

		return exit;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Calls <see cref="IServer.Start"/> now and calls <see cref="IServer.Stop"/> in <see cref="IServerHost.Exit"/>.
	/// </summary>
	/// <param name="server">A server instance.</param>
	/// <exception cref="ArgumentNullException"><paramref name="server"/> is null.</exception>
	/// <exception cref="ObjectDisposedException">If <see cref="Dispose()"/> has been called already.</exception>
	/// <exception cref="InvalidOperationException">If <see cref="IServerHost.Exit"/> has started already.</exception>
	public void Add(IServer server)
	{
		if (server == null)
		{
			throw new ArgumentNullException(nameof(server));
		}

		this.EnsureReady();

		bool start = false;
		lock (this.servers)
		{
			if (this.servers.Add(server))
			{
				start = true;
			}
		}

		if (start)
		{
			server.Start();
		}
	}

	/// <summary>
	/// Waits for all added <see cref="IServer"/> instances to stop after <see cref="IServerHost.Exit"/> is started.
	/// </summary>
	public void WaitForExit() => this.resetEvent.Wait();

	#endregion

	#region Private Methods

	private void StartExiting()
	{
		if (!this.isExiting)
		{
			this.isExiting = true;

			List<IServer> stopServers;
			lock (this.servers)
			{
				stopServers = new(this.servers.Count);
				foreach (IServer server in this.servers)
				{
					server.Stopped += this.Server_Stopped;

					// We must stop the servers outside the lock because their Stopped
					// callback could immediately come back in on the same thread,
					// which would try to remove the server from the collection we're
					// iterating through.
					stopServers.Add(server);
				}
			}

			if (stopServers.Count == 0)
			{
				this.FinishExiting();
			}
			else
			{
				foreach (IServer server in stopServers)
				{
					// Tell the server to stop any waiting listeners and don't start new ones.
					// When all the connected listeners finish, the Stopped event will be raised.
					// That will invoke FinishExiting to let the process finish.
					server.Stop();
				}
			}
		}
	}

	private void FinishExiting()
		=> this.resetEvent.Set();

	private void Server_Stopped(object? sender, EventArgs e)
	{
		if (sender is IServer server)
		{
			server.Stopped -= this.Server_Stopped;

			bool isFinished = false;
			lock (this.servers)
			{
				if (this.servers.Remove(server))
				{
					isFinished = this.servers.Count == 0;
				}
			}

			if (isFinished)
			{
				this.FinishExiting();
			}
		}
	}

	private void Dispose(bool disposing)
	{
		if (!this.isDisposed)
		{
			if (disposing)
			{
				this.resetEvent.Dispose();
			}

			lock (this.servers)
			{
				this.servers.Clear();
			}

			this.isDisposed = true;
		}
	}

	private void EnsureReady([CallerMemberName] string? callerMemberName = null)
	{
		if (this.isDisposed)
		{
			throw new ObjectDisposedException(nameof(ServerHost));
		}

		if (this.isExiting)
		{
			throw new InvalidOperationException($"{callerMemberName} can't be called after {nameof(IServerHost.Exit)} starts.");
		}
	}

	#endregion
}
