namespace ServerHost;

public interface IServerHost
{
	bool IsReady { get; }

	void Shutdown();
}
