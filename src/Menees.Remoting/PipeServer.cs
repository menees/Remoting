namespace Menees.Remoting;

#region Using Directives

using System.IO.Pipes;

#endregion

internal sealed class PipeServer : PipeBase
{
	#region Private Data Members

	private readonly HashSet<PipeServerListener> listeners = new();
	private readonly int minListeners;
	private readonly int maxListeners;

	#endregion

	#region Constructors

	internal PipeServer(string pipeName, int minListeners, int maxListeners, Action<Stream> processRequest)
		: base(pipeName)
	{
		if (minListeners <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minListeners), $"{nameof(minListeners)} must be positive.");
		}

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
		this.ProcessRequest = processRequest;

		// Note: We don't create any listeners here in the constructor because we want to finish construction first.
		// If we created even one listener here, then it could start processing on a worker thread and immediately
		// call back in to EnsureMinListeners, which would mean a listener was using a partially constructed PipeServer. Yuck.
		// It's safer, better, and easier to reason about if we require a separate post-constructor call to EnsureMinListeners.
	}

	#endregion

	#region Public Properties

	public Action<Stream> ProcessRequest { get; }

	public Action<Exception>? ReportUnhandledException { get; set; }

	#endregion

	#region Internal Methods

	internal void EnsureMinListeners()
	{
		lock (this.listeners)
		{
			foreach (PipeServerListener listener in this.listeners.ToList())
			{
				// Listeners normally self-dispose, so we can just let go of them here.
				if (listener.State == ListenerState.Disposed)
				{
					this.listeners.Remove(listener);
				}
			}

			int currentCount = this.listeners.Count;
			if (currentCount < this.maxListeners)
			{
				int waitingCount = this.listeners.Count(listener => listener.State == ListenerState.WaitingForConnection);
				if (waitingCount < this.minListeners)
				{
					int createCount = Math.Min(this.minListeners - waitingCount, this.maxListeners - currentCount);
					for (int i = 0; i < createCount; i++)
					{
						NamedPipeServerStream pipe = new(this.PipeName, Direction, this.maxListeners, Mode, Options);
						PipeServerListener listener = new(this, pipe);
						this.listeners.Add(listener);
					}
				}
			}
		}
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
