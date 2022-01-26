﻿namespace Menees.Remoting;

#region Using Directives

using System.Reflection;

#endregion

/// <summary>
/// Used to invoke a <typeparamref name="TServiceInterface"/> interface member on a <see cref="RmiServer{T}"/>.
/// </summary>
/// <typeparam name="TServiceInterface">The interface to remotely invoke members on.</typeparam>
public sealed class RmiClient<TServiceInterface> : RmiBase<TServiceInterface>
	where TServiceInterface : class
{
	#region Private Data Members

	private readonly PipeClient pipe;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new client instance to invoke methods on a <see cref="RmiServer{TServiceInterface}"/> instance.
	/// </summary>
	/// <param name="serverPath">The path used to expose the service.</param>
	/// <param name="serverHost">The name of the remote server machine. Use "." for the local system.</param>
	/// <param name="serializer">An optional custom serializer.
	/// Note: The associated <see cref="RmiServer{TServiceInterface}"/> instance must use a compatible serializer.
	/// </param>
	/// <param name="connectTimeout">The interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// If null, then <see cref="DefaultConnectTimeout"/> is used.</param>
	public RmiClient(
		string serverPath,
		string serverHost = ".",
		ISerializer? serializer = null,
		TimeSpan? connectTimeout = null)
		: base(serializer)
	{
		this.ConnectTimeout = connectTimeout ?? DefaultConnectTimeout;
		this.pipe = new(serverPath, serverHost);
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets the default interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// </summary>
	public static TimeSpan DefaultConnectTimeout { get; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets the interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// </summary>
	public TimeSpan ConnectTimeout { get; }

	#endregion

	#region Public Methods

	/// <summary>
	/// Creates a <typeparamref name="TServiceInterface"/> proxy that remotely
	/// invokes members on an <see cref="RmiServer{TServiceInterface}"/> using the
	/// path passed to this <see cref="RmiClient{TServiceInterface}"/>'s constructor.
	/// </summary>
	/// <returns>A new proxy instance associated with this client.</returns>
	public TServiceInterface CreateProxy()
	{
		TServiceInterface result = DispatchProxy.Create<TServiceInterface, ClientProxy<TServiceInterface>>();
		if (result is not ClientProxy<TServiceInterface> proxy)
		{
			throw new InvalidOperationException("Unsupported proxy type.");
		}

		proxy.Initialize(this);
		return result;
	}

	#endregion

	#region Internal Methods

	internal object? Invoke(MethodInfo targetMethod, object?[] args)
	{
		Request request = CreateRequest(targetMethod, args);

		Response? response = null;
		this.pipe.SendRequest(this.ConnectTimeout, stream =>
		{
			request.WriteTo(stream, this.Serializer);
			response = Message.ReadFrom<Response>(stream, this.Serializer);
		});

		if (response != null && response.IsServiceException && response.ReturnValue is Exception ex)
		{
			// Try to throw a new exception of the same type so the outer exception's StackTrace will be from the client,
			// and the inner exception's StackTrace will be from the server.
			Type exceptionType = ex.GetType();
			ConstructorInfo? constructor = exceptionType.GetConstructor(new[] { typeof(string), typeof(Exception) });
			if (constructor != null)
			{
				throw (Exception)constructor.Invoke(new object?[] { ex.Message, ex });
			}
			else
			{
				// We have to rethrow the returned service exception, so this will not preserve the server's StackTrace.
				throw ex;
			}
		}

		object? result = response?.ReturnValue;
		return result;
	}

	#endregion

	#region Protected Methods

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			this.pipe.Dispose();
		}
	}

	#endregion

	#region Private Methods

	private static Request CreateRequest(MethodInfo targetMethod, object?[] args)
	{
		int argCount = args.Length;
		List<(object? Value, Type DataType)> arguments = new(argCount);
		ParameterInfo[]? parameters = null;

		for (int i = 0; i < argCount; i++)
		{
			object? value = args[i];
			Type? dataType = value?.GetType();
			if (dataType == null)
			{
				parameters ??= targetMethod.GetParameters();
				if (i < parameters.Length)
				{
					dataType = parameters[i].ParameterType;
				}
			}

			dataType ??= typeof(object);
			arguments.Add((value, dataType));
		}

		Request request = new()
		{
			MethodSignature = GetMethodSignature(targetMethod),
			Arguments = arguments,
		};

		return request;
	}

	#endregion
}
