namespace ServerHost;

// TODO: Move this to Remoting library? [Bill, 2/23/2022]
// TODO: Rename to IServerManager with ServerManager implementation.  [Bill, 2/23/2022]
internal sealed class ServerHostManager : IServerHost
{
	#region Private Data Members

	private readonly ManualResetEventSlim resetEvent = new(false);

	#endregion

	#region Public Properties

	public bool IsReady => !this.resetEvent.IsSet;

	// TODO: Change this to AddServer and support a collection. [Bill, 2/23/2022]
	public IServer? Server { get; set; }

	#endregion

	#region Public Methods

	// Give the IServerHost.Shutdown() client a little time to receive our response and disconnect.
	// Otherwise, this process could end too soon, and the client would get an ArgumentException
	// like "Unable to read 4 byte message length from stream. Only 0 bytes were available.".
	public void Shutdown() => this.BeginShutdown();

	public void WaitForShutdown() => this.resetEvent.Wait();

	#endregion

	#region Private Methods

	private void BeginShutdown()
	{
		if (this.Server != null)
		{
			this.Server.Stopped += this.Server_Stopped;

			// Tell the server to stop any waiting listeners and don't start new ones.
			// When all the connected listeners finish, the Stopped action will be called
			// to invoke EndShutdown to let the process finish.
			this.Server.Stop();
		}
		else
		{
			this.EndShutdown();
		}
	}

	private void EndShutdown()
	{
		if (this.Server != null)
		{
			this.Server.Stopped -= this.Server_Stopped;
		}

		this.resetEvent.Set();
	}

	// TODO: Only call EndShutdown when all servers have stopped. [Bill, 2/23/2022]
	private void Server_Stopped(object? sender, EventArgs e)
		=> this.EndShutdown();

	#endregion
}
