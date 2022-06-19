namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Menees.Remoting.Pipes;
using Menees.Remoting.Security;

#endregion

[TestClass]
public class RmiServerTests : BaseTests
{
	#region Public Methods

	[TestMethod]
	public void CloneString()
	{
		string serverPath = this.GenerateServerPath();
		string expected = Guid.NewGuid().ToString();
		using RmiServer<ICloneable> server = new(expected, serverPath, loggerFactory: this.LoggerFactory);

		// This is a super weak, insecure example since it just checks for the word "System".
		server.TryGetType = typeName => typeName.Contains(nameof(System))
			? Type.GetType(typeName, true)
			: throw new ArgumentException("TryGetType disallowed " + typeName);

		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<ICloneable> client = new(serverPath, loggerFactory: this.LoggerFactory);
		ICloneable proxy = client.CreateProxy();
		string actual = (string)proxy.Clone();
		actual.ShouldBe(expected);
	}

	[TestMethod]
	public void SingleServer()
	{
		// Run 20 clients that have to wait on a single server listener.
		this.TestClient(1, 1, 20);
	}

	[TestMethod]
	public void MultiServerMedium()
	{
		// Run 100 clients that will have to wait on the 4-20 server listeners.
		this.TestClient(4, 20, 100);
	}

	[TestMethod]
	public void MultiServerLarge()
	{
		// Run 5000 clients that will have to wait on the 20-100 server listeners.
		this.TestClient(20, 100, 5000);
	}

	[TestMethod]
	public void UnlimitedServerMedium()
	{
		// Run 500 clients that will have to wait on the available server listeners.
		this.TestClient(1, ServerSettings.MaxAllowedListeners, 500);
	}

	[TestMethod]
	public void InProcessServer()
	{
		string serverPath = this.GenerateServerPath();
		using ServerHost host = new();
		using RmiServer<IServerHost> server = new(host, serverPath, 2, 2, loggerFactory: this.LoggerFactory);
		server.ReportUnhandledException = WriteUnhandledServerException;
		host.Add(server);

		using RmiClient<IServerHost> client = new(serverPath, connectTimeout: TimeSpan.FromSeconds(2), loggerFactory: this.LoggerFactory);
		IServerHost proxy = client.CreateProxy();
		IServerHost direct = host;
		proxy.IsReady.ShouldBeTrue();
		direct.IsReady.ShouldBeTrue();
		host.ExitCode.ShouldBeNull();
		proxy.Exit(0);
		host.ExitCode.ShouldBe(0);
		host.WaitForExit();

		direct.IsReady.ShouldBeFalse();

		// The proxy interface isn't usable now since we told the host to Exit.
		Should.Throw<TimeoutException>(() => proxy.IsReady.ShouldBeFalse());
	}

	[TestMethod]
	public async Task CrossProcessServerAsync()
	{
		await this.TestCrossProcessServerAsync(
			this.GenerateServerPathPrefix(),
			async prefix =>
			{
				this.TestClient(50, $"{prefix}{nameof(ITester)}");
				await Task.CompletedTask.ConfigureAwait(false);
			},
			20,
			4).ConfigureAwait(false);
	}

	[TestMethod]
	public void SecurityVariations()
	{
		const int ClientCount = 2;
		this.TestClient(1, 1, ClientCount, PipeClientSecurity.CurrentUserOnly);
		this.TestClient(1, 1, ClientCount, serverSecurity: PipeServerSecurity.CurrentUserOnly);
		this.TestClient(1, 1, ClientCount, PipeClientSecurity.CurrentUserOnly, PipeServerSecurity.CurrentUserOnly);

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			try
			{
				// This empty PipeSecurity instance doesn't grant any user access to the pipe (even the current user).
				PipeSecurity pipeSecurity = new();
				this.TestClient(1, 1, ClientCount, PipeClientSecurity.CurrentUserOnly, new PipeServerSecurity(pipeSecurity));
				Assert.Fail("Client should not have access to connect to server.");
			}
			catch (AggregateException ex)
			{
				// Depending on how TestClient's Parallel.ForEach fails, we may get 1 or 2 UnauthorizedAccessExceptions.
				ex.InnerExceptions.Any(e => e is UnauthorizedAccessException).ShouldBeTrue();
			}
		}
	}

	#endregion

	#region Private Methods

	private void TestClient(
		int minServerListeners,
		int maxServerListeners,
		int clientCount,
		ClientSecurity? clientSecurity = null,
		ServerSecurity? serverSecurity = null,
		[CallerMemberName] string? callerMemberName = null)
	{
		string serverPath = this.GenerateServerPath(callerMemberName);

		ServerSettings serverSettings = new(serverPath)
		{
			MaxListeners = maxServerListeners,
			MinListeners = minServerListeners,
			CreateLogger = this.LoggerFactory.CreateLogger,
			Security = serverSecurity,
		};

		Tester tester = new();
		using ServerHost host = new();
		using RmiServer<ITester> server = new(tester, serverSettings);
		server.ReportUnhandledException = WriteUnhandledServerException;
		host.Add(server);

		this.TestClient(clientCount, serverPath, clientSecurity);

		// Make sure all the servers have completely exited in case another TestClient
		// starts up immediately using the same serverPath. We don't want a new client
		// to race in an grab an old server listener just as its shutting down.
		((IServerHost)host).Exit();
		host.WaitForExit();
	}

	private void TestClient(int clientCount, string serverPath, ClientSecurity? clientSecurity = null)
	{
		TimeSpan timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : ClientSettings.DefaultConnectTimeout;
		Parallel.ForEach(
			Enumerable.Range(1, clientCount),
			new ParallelOptions { MaxDegreeOfParallelism = Math.Min(clientCount, 8 * Environment.ProcessorCount) },
			item =>
			{
				ClientSettings clientSettings = new(serverPath)
				{
					ConnectTimeout = timeout,
					CreateLogger = this.LoggerFactory.CreateLogger,
					Security = clientSecurity,
				};

				using RmiClient<ITester> client = new(clientSettings);
				ITester proxy = client.CreateProxy();
				TestProxy(proxy, item, isSingleClient: clientCount == 1);

				const string Prefix = "Item";
				string actual = proxy.Combine(Prefix, item.ToString());
				actual.ShouldBe(Prefix + item);
			});
	}

	#endregion
}
