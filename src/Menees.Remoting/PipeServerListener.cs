namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO.Pipes;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal sealed class PipeServerListener : IDisposable
	{
		#region Private Data Members

		private readonly PipeServer server;
		private readonly NamedPipeServerStream pipe;

		private IAsyncResult? pendingConnection;
		private bool disposed;

		#endregion

		#region Constructors

		public PipeServerListener(PipeServer server, NamedPipeServerStream pipe)
		{
			this.server = server;
			this.pipe = pipe;
			this.State = ListenerState.WaitingForConnection;
			this.pendingConnection = this.pipe.BeginWaitForConnection(this.OnConnected, null);
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
					this.pipe.Disconnect();
				}

				this.pipe.Dispose();
			}
		}

		#endregion

		#region Private Methods

		private void OnConnected(IAsyncResult result)
		{
			// Per the NamedPipeServerInstance example at https://www.codeproject.com/Articles/1199046/A-Csharp-Named-Pipe-Library-That-Supports-Multiple,
			// this callback will be invoked if the listener is disposed while it's still waiting for a connection. In that case, we should do nothing.
			if (!this.disposed && this.pendingConnection != null)
			{
				this.pipe.EndWaitForConnection(this.pendingConnection);
				this.pendingConnection = null;
				this.State = ListenerState.Connected;

				// Since this listener is now connected (and about to begin processing a request), tell the server so it
				// can start another listener if necessary. If the server is already at its max, it may not be able to.
				this.server.EnsureMinListeners();

				// Process the incoming request on a pool thread using a background task.
				Task processingRequest = Task.Run(this.ProcessRequest);

				// After processing the request, then we should clean everything up.
				processingRequest.ContinueWith(this.StopListening, TaskScheduler.Default);
			}
		}

		private void ProcessRequest()
		{
			if (!this.disposed)
			{
				this.State = ListenerState.ProcessingRequest;
				try
				{
					// TODO: Finish ProcessRequest. [Bill, 1/24/2022]
					this.GetHashCode();
				}
				catch (Exception ex)
				{
					// TODO: Propagate exception back to caller. [Bill, 1/24/2022]
					ex.GetHashCode();
				}
			}
		}

		private void StopListening(Task processingRequest)
		{
			if (!this.disposed)
			{
				this.State = ListenerState.FinishedRequest;

				// Make sure any task exception is observed. This shouldn't occur if ProcessRequest
				// handles all exceptions, but we'll pass this on just to be as careful as possible.
				// https://devblogs.microsoft.com/pfxteam/task-exception-handling-in-net-4-5/
				if (processingRequest.IsFaulted && processingRequest.Exception != null)
				{
					this.server.ReportUnhandledException(processingRequest.Exception);
				}

				processingRequest.Dispose();

				// Self dispose since each listener should only be used for a single request.
				this.Dispose();

				// Poke the server to indicate it should start another listener if necessary.
				// If it was at its max earlier when we started processing, then maybe now
				// that we're finished it'll be below the max (unless another thread snuck in
				// and started a new listener).
				this.server.EnsureMinListeners();
			}
		}

		#endregion
	}
}
