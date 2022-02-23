namespace ServerHost;

internal sealed class ServerHostManager : IServerHost
{
	private readonly ManualResetEventSlim resetEvent = new(false);

	public bool IsReady => !this.resetEvent.IsSet;

	public IServer? Server { get; set; }

	// Give the IServerHost.Shutdown() client a little time to receive our response and disconnect.
	// Otherwise, this process could end too soon, and the client would get an ArgumentException
	// like "Unable to read 4 byte message length from stream. Only 0 bytes were available.".
	public void Shutdown() => this.Shutdown(this.Server == null);

	public void Shutdown(bool setEvent)
	{
		if (setEvent)
		{
			this.resetEvent.Set();
		}
		else if (this.Server != null)
		{
			// Tell the server to stop any waiting listeners and don't start new ones.
			// When all the connected listeners finish, the Stopped action will be called
			// to invoke Shutdown(true) to let the process finish.
			this.Server.Stop();
		}
	}

	public void WaitForShutdown() => this.resetEvent.Wait();
}
