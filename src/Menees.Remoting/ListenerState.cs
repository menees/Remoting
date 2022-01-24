namespace Menees.Remoting
{
	internal enum ListenerState
	{
		WaitingForConnection,
		Connected,
		ProcessingRequest,
		FinishedRequest,
		Disposed,
	}
}
