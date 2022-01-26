namespace ServerHost;

internal sealed class ServerHostManager : IServerHost
{
	private readonly ManualResetEventSlim resetEvent = new(false);

	public void Shutdown() => this.resetEvent.Set();

	public void WaitForShutdown() => this.resetEvent.Wait();
}
