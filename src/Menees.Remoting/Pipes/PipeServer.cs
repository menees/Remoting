namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeServer : PipeNode
{
	#region Private Data Members

	private readonly HashSet<PipeServerListener> listeners = new();
	private readonly int minListeners;
	private readonly int maxListeners;
	private readonly IServer server;
	private readonly PipeServerSecurity? security;
	private bool stopping;

	#endregion

	#region Constructors

	internal PipeServer(
		string pipeName,
		int minListeners,
		int maxListeners,
		Func<Stream, Task> processRequestAsync,
		IServer server,
		Node owner,
		PipeServerSecurity? security)
		: base(pipeName, owner)
	{
		if (minListeners <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minListeners), $"{nameof(minListeners)} must be positive.");
		}

		// Stephen Toub made a comment "that Windows supports this across processes",
		// and it seems to imply that Unix doesn't. Maybe it's a per process limit in Unix.
		// https://github.com/dotnet/corefx/pull/24798#issuecomment-338809086
		if (maxListeners != NamedPipeServerStream.MaxAllowedServerInstances)
		{
			if (maxListeners <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(maxListeners),
					$"{nameof(maxListeners)} must be positive or {nameof(NamedPipeServerStream.MaxAllowedServerInstances)}.");
			}
			else if (maxListeners < minListeners)
			{
				throw new ArgumentOutOfRangeException(nameof(maxListeners), $"{nameof(maxListeners)} must be >= {nameof(minListeners)}.");
			}
		}

		this.minListeners = minListeners;
		this.maxListeners = maxListeners;
		this.ProcessRequestAsync = processRequestAsync;
		this.server = server;
		this.security = security;

		// Note: We don't create any listeners here in the constructor because we want to finish construction first.
		// If we created even one listener here, then it could start processing on a worker thread and immediately
		// call back in to EnsureMinListeners, which would mean a listener was using a partially constructed PipeServer. Yuck.
		// It's safer, better, and easier to reason about if we require a separate post-constructor call to EnsureMinListeners.
	}

	#endregion

	#region Public Events

	/// <inheritdoc/>
	public event EventHandler? Stopped;

	#endregion

	#region Public Properties

	public Func<Stream, Task> ProcessRequestAsync { get; }

	public Action<Exception>? ReportUnhandledException { get; set; }

	#endregion

	#region Internal Methods

	internal void EnsureMinListeners()
	{
		this.Logger.LogTrace("About to request server listener lock.");
		lock (this.listeners)
		{
			this.Logger.LogTrace("Initial listeners: {Count}", this.listeners.Count);

			// Make a list copy of the hashset since we may need to remove members from the hashset.
			foreach (PipeServerListener listener in this.listeners.ToList())
			{
				// Listeners normally self-dispose, so we can just let go of them here.
				if (listener.State == ListenerState.Disposed)
				{
					this.listeners.Remove(listener);
				}
			}

			this.Logger.LogTrace("Non-disposed listeners: {Count}", this.listeners.Count);

			if (this.stopping)
			{
				if (this.listeners.Count == 0)
				{
					// This should only happen once since we'll only go from 1 to 0 once after this.stopping.
					this.Stopped?.Invoke(this.server, EventArgs.Empty);
				}
			}
			else
			{
				int availableListeners = (this.maxListeners == NamedPipeServerStream.MaxAllowedServerInstances
					? int.MaxValue : this.maxListeners) - this.listeners.Count;

				this.Logger.LogTrace("Available listeners: {Count}", availableListeners);
				if (availableListeners > 0)
				{
					int waitingCount = this.listeners.Count(listener => listener.State <= ListenerState.WaitingForConnection);
					this.Logger.LogTrace("Waiting listeners: {Count}", waitingCount);
					if (waitingCount < this.minListeners)
					{
						int createCount = Math.Min(this.minListeners - waitingCount, availableListeners);
						this.Logger.LogTrace("Create listeners: {Count}", createCount);
						for (int i = 0; i < createCount; i++)
						{
							NamedPipeServerStream? pipe = null;
							try
							{
								// Pass the actual maxListeners value to the new pipe since it's externally visible using SysInternals' PipeList.
								pipe = this.security?.CreatePipe(this.PipeName, Direction, this.maxListeners, Mode, PipeOptions.Asynchronous)
									?? new(this.PipeName, Direction, this.maxListeners, Mode, PipeOptions.Asynchronous);
							}
							catch (IOException ex)
							{
								// We can get "All pipe instances are busy." if we reached the requested max limit or hit an OS limit.
								this.Logger.LogDebug(ex, "Unable to create new named pipe server.");
							}

							if (pipe == null)
							{
								// "Merry Christmas. Shitter's full." https://www.youtube.com/watch?v=BeskbiJjCXI#t=21s
								break;
							}

							PipeServerListener listener = new(this, pipe, this.Owner);
							this.listeners.Add(listener);

							// Note: We're intentionally not waiting on this to return. This is a true fire-and-forget case.
							// When an I/O thread signals the listener that a client has connected, that listener will
							// call back into us to start a new listener when available.
#pragma warning disable VSTHRD110 // Observe result of async calls. Intentionally fire-and-forget.
							listener.StartAsync().ConfigureAwait(false);
#pragma warning restore VSTHRD110 // Observe result of async calls
						}
					}
				}
			}
		}
	}

	internal void StopListening()
	{
		this.Logger.LogTrace("Stopping.");
		lock (this.listeners)
		{
			this.stopping = true;

			this.Logger.LogTrace("Initial listeners during stop: {Count}", this.listeners.Count);
			foreach (PipeServerListener listener in this.listeners.ToList())
			{
				if (listener.State <= ListenerState.WaitingForConnection)
				{
					listener.Dispose();
					this.listeners.Remove(listener);
				}
			}

			this.Logger.LogTrace("Non-disposed listeners during stop: {Count}", this.listeners.Count);
		}

		// Call this now that this.stopping is set, so it can invoke this.Stopped if there are no more listeners.
		this.EnsureMinListeners();
	}

	#endregion

	#region Protected Methods

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		lock (this.listeners)
		{
			foreach (PipeServerListener listener in this.listeners)
			{
				listener.Dispose();
			}

			this.listeners.Clear();
		}
	}

	#endregion
}
