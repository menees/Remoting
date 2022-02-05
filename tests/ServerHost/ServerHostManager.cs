namespace ServerHost;

internal sealed class ServerHostManager : IServerHost
{
	private readonly ManualResetEventSlim resetEvent = new(false);

	public bool IsReady => !this.resetEvent.IsSet;

	public void Shutdown() => this.resetEvent.Set();

	public void WaitForShutdown() => this.resetEvent.Wait();
}
