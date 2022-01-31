namespace Menees.Remoting.Pipes;

internal enum ListenerState
{
	Created,
	WaitingForConnection,
	Connected,
	ProcessingRequest,
	FinishedRequest,
	Disposed,
}
