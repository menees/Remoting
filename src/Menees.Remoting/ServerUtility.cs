namespace Menees.Remoting;

#region Using Directives

using Menees.Remoting.Models;
using Microsoft.Extensions.Logging;

#endregion

internal static class ServerUtility
{
	#region Public Methods

	public static Response CreateResponse(Exception ex) => new() { Error = new(ex) };

	public static async Task ProcessRequestAsync(
		Node node,
		IServer server,
		Stream clientStream,
		Func<Request, CancellationToken, Task<Response>> processRequestAsync,
		CancellationToken cancellationToken)
	{
		Response response;

		try
		{
			Request request = await Message.ReadFromAsync<Request>(clientStream, node.SystemSerializer, cancellationToken).ConfigureAwait(false);
			response = await processRequestAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			node.CreateLogger(node.GetType()).LogError(ex, "Exception while server was processing a request.");
			server.ReportUnhandledException?.Invoke(ex);

			try
			{
				// Try to report the original exception.
				response = CreateResponse(ex);
			}
			catch (Exception ex2)
			{
				// If we couldn't serialize the original exception, try to return a simple error with just the messages.
				response = CreateResponse(new InvalidOperationException(string.Join(Environment.NewLine, ex.Message, ex2.Message)));
			}
		}

		try
		{
			await response.WriteToAsync(clientStream, node.SystemSerializer, cancellationToken).ConfigureAwait(false);
		}
		catch (ObjectDisposedException ex)
		{
			// We can get an ObjectDisposedException("Cannot access a closed pipe.")
			// when WriteToAsync calls FlushAsync() on the stream if the client has already
			// received all the data, processed it quickly, and closed the pipe. That's ok.
			node.CreateLogger(node.GetType()).LogDebug(ex, "Client closed connection while server was finishing write.");
		}
		catch (Exception ex)
		{
			node.CreateLogger(node.GetType()).LogError(ex, "Unhandled exception while server was finishing write.");
			server.ReportUnhandledException?.Invoke(ex);
		}
	}

	#endregion
}
