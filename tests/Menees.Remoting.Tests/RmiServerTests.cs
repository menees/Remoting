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
	#region Private Interfaces

	private interface IRoot
	{
		int GetRoot(int input);
	}

	private interface ILevel1A : IRoot
	{
		int GetLevel1A(int input);
	}

	private interface ILevel1B : IRoot
	{
		int GetLevel1B(int input);
	}

	private interface IDiamond : ILevel1A, ILevel1B
	{
		int GetDiamond(int input);
	}

	#endregion

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

		// When this test runs by itself a short timeout is ok. When run with other tests that use a lot of
		// client connections (e.g., MessageNodeTests.StringToCodeNameInProcessAsync), then this client's
		// proxies on .NET Framework can get a semaphore timeout inside NamedPipeClientStream.Connect.
		// That doesn't happen with .NET Core. Since we intentionally test for a TimeoutException at the
		// end, we want this timeout to be as short as we can get away with.
		TimeSpan connectTimeout = TimeSpan.FromSeconds(IsDotNetFramework ? 10 : 2);
		using RmiClient<IServerHost> client = new(serverPath, connectTimeout, loggerFactory: this.LoggerFactory);
		IServerHost proxy = client.CreateProxy();
		IServerHost direct = host;
		proxy.IsReady.ShouldBeTrue();
		direct.IsReady.ShouldBeTrue();

		// Test IServerHost.Exit method and ServerHost.Exiting event.
		host.ExitCode.ShouldBeNull();
		bool allowExit = false;
		host.Exiting += (s, e) => e.Cancel = !allowExit;
		proxy.Exit(0).ShouldBeFalse();
		host.ExitCode.ShouldBeNull();
		direct.IsReady.ShouldBeTrue();
		allowExit = true;
		proxy.Exit(0).ShouldBeTrue();
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

	[TestMethod]
	public void InterfaceInheritance()
	{
		string serverPath = this.GenerateServerPath();
		Diamond diamond = new();
		using RmiServer<IDiamond> server = new(diamond, serverPath, loggerFactory: this.LoggerFactory);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<IDiamond> client = new(serverPath, loggerFactory: this.LoggerFactory);
		IDiamond proxy = client.CreateProxy();
		proxy.GetRoot(10).ShouldBe(10);
		proxy.GetLevel1A(10).ShouldBe(110);
		proxy.GetLevel1B(10).ShouldBe(210);
		proxy.GetDiamond(10).ShouldBe(1010);
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

	#region Private Types

	private sealed class Diamond : IDiamond
	{
		public int GetDiamond(int input) => input + 1000;

		public int GetLevel1A(int input) => input + 100;

		public int GetLevel1B(int input) => input + 200;

		public int GetRoot(int input) => input;
	}

	#endregion
}
