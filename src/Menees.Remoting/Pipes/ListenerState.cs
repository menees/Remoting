namespace Menees.Remoting;

internal enum ListenerState
{
	Created,
	WaitingForConnection,
	Connected,
	ProcessingRequest,
	FinishedRequest,
	Disposed,
}
