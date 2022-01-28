﻿namespace Menees.Remoting;

#region Using Directives

using System.IO.Pipes;
using System.Reflection;

#endregion

/// <summary>
/// Exposes the <typeparamref name="TServiceInterface"/> interface from a given service object instance
/// as a remotely invokable server.
/// </summary>
/// <typeparam name="TServiceInterface">The interface to make available for remote invocation.</typeparam>
public sealed class RmiServer<TServiceInterface> : RmiBase<TServiceInterface>, IRmiServer
	where TServiceInterface : class
{
	#region Private Data Members

	private static readonly Dictionary<string, MethodInfo> MethodSignatureCache =
		typeof(TServiceInterface).GetMethods().ToDictionary(method => GetMethodSignature(method));

	private readonly PipeServer pipe;

	private TServiceInterface? serviceInstance;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new server instance with the specified name
	/// </summary>
	/// <param name="serverPath">The path used to expose the service.</param>
	/// <param name="serviceInstance">An instance of <typeparamref name="TServiceInterface"/> on which to execute remote invocations.
	/// </param>
	/// <param name="maxListeners">The maximum number of server listener tasks to start.</param>
	/// <param name="minListeners">The minimim number of server listener tasks to start.</param>
	/// <param name="serializer">An optional custom serializer.
	/// Note: All connecting <see cref="RmiClient{TServiceInterface}"/> instances must use a compatible serializer.
	/// </param>
	public RmiServer(
		string serverPath,
		TServiceInterface serviceInstance,
		int maxListeners = NamedPipeServerStream.MaxAllowedServerInstances,
		int minListeners = 1,
		ISerializer? serializer = null)
		: base(serializer)
	{
		this.serviceInstance = serviceInstance;

		// Note: The pipe is created with no listeners until we explicitly start them.
		this.pipe = new(serverPath, minListeners, maxListeners, this.ProcessRequest);
	}

	#endregion

	#region Public Properties

	/// <inheritdoc/>
	public Action<Exception>? ReportUnhandledException
	{
		get => this.pipe.ReportUnhandledException;
		set => this.pipe.ReportUnhandledException = value;
	}

	#endregion

	#region Public Methods

	/// <inheritdoc/>
	public void Start() => this.pipe.EnsureMinListeners();

	#endregion

	#region Protected Methods

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			this.serviceInstance = null;
			this.pipe.Dispose();
		}
	}

	#endregion

	#region Private Methods

	private static Response CreateResponse(Exception ex)
	{
		Response response = new()
		{
			IsServiceException = true,
			ReturnValue = ex,
			ReturnType = ex.GetType(),
		};

		return response;
	}

	private void ProcessRequest(Stream clientStream)
	{
		Response response;

		try
		{
			Request request = Message.ReadFrom<Request>(clientStream, this.Serializer);

			if (!MethodSignatureCache.TryGetValue(request.MethodSignature ?? string.Empty, out MethodInfo? method))
			{
				response = CreateResponse(new TargetException(
					$"A {typeof(TServiceInterface).FullName} method with signature '{request.MethodSignature}' was not found."));
			}
			else if (this.serviceInstance is not object target)
			{
				response = CreateResponse(new ObjectDisposedException(this.GetType().FullName));
			}
			else
			{
				IEnumerable<(object? Value, Type DataType)> args = request.Arguments ?? Enumerable.Empty<(object? Value, Type DataType)>();
				object? methodResult;
				try
				{
					methodResult = method.Invoke(target, args.Select(tuple => tuple.Value).ToArray());
					response = new Response
					{
						ReturnValue = methodResult,
						ReturnType = methodResult?.GetType() ?? method.ReturnType,
					};
				}
				catch (TargetInvocationException ex)
				{
					// The inner exception is the original exception thrown by the invoked method.
					response = CreateResponse(ex.InnerException ?? ex);
					methodResult = null;
				}

				// The inner try..catch for Invoke only handles TargetInvocationException so any exceptions from
				// invalid request arguments or from trying to serialize methodResult will pass through the
				// ReportUnhandledException action below before being returned.
			}
		}
		catch (Exception ex)
		{
			this.ReportUnhandledException?.Invoke(ex);

			try
			{
				// Try to serialize the original exception.
				response = CreateResponse(ex);
			}
			catch (Exception ex2)
			{
				// If we couldn't serialize the original exception, try to return a simple exception with just the messages.
				response = CreateResponse(new InvalidOperationException(string.Join(Environment.NewLine, ex.Message, ex2.Message)));
			}
		}

		response.WriteTo(clientStream, this.Serializer);
	}

	#endregion
}
