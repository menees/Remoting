namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.Runtime.CompilerServices;

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
		using RmiServer<ICloneable> server = new(expected, serverPath, loggerFactory: this.Loggers);

		// This is a super weak, insecure example since it just checks for the word "System".
		server.TryGetType = typeName => typeName.Contains(nameof(System))
			? Type.GetType(typeName, true)
			: throw new ArgumentException("TryGetType disallowed " + typeName);

		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<ICloneable> client = new(serverPath, loggerFactory: this.Loggers);
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
		InProcServerHost host = new();
		using RmiServer<IServerHost> server = new(host, serverPath, 2, 2, loggerFactory: this.Loggers);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<IServerHost> client = new(serverPath, loggerFactory: this.Loggers);
		IServerHost proxy = client.CreateProxy();
		host.IsReady.ShouldBeTrue();
		host.ExitCode.ShouldBeNull();
		proxy.Exit(0);
		host.IsReady.ShouldBeFalse();
		host.ExitCode.ShouldBe(0);
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

	// TODO: Add RMI test with client and server security. [Bill, 2/20/2022]
	#endregion

	#region Private Methods

	private void TestClient(
		int minServerListeners,
		int maxServerListeners,
		int clientCount,
		[CallerMemberName] string? callerMemberName = null)
	{
		string serverPath = this.GenerateServerPath(callerMemberName);

		Tester tester = new();
		using RmiServer<ITester> server = new(tester, serverPath, maxServerListeners, minServerListeners, loggerFactory: this.Loggers);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		this.TestClient(clientCount, serverPath);
	}

	private void TestClient(int clientCount, string serverPath)
	{
		TimeSpan timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : ClientSettings.DefaultConnectTimeout;
		Parallel.ForEach(
			Enumerable.Range(1, clientCount),
			new ParallelOptions { MaxDegreeOfParallelism = Math.Min(clientCount, 8 * Environment.ProcessorCount) },
			item =>
			{
				using RmiClient<ITester> client = new(serverPath, connectTimeout: timeout, loggerFactory: this.Loggers);
				ITester proxy = client.CreateProxy();
				RmiClientTests.TestProxy(proxy, item, isSingleClient: clientCount == 1);

				const string Prefix = "Item";
				string actual = proxy.Combine(Prefix, item.ToString());
				actual.ShouldBe(Prefix + item);
			});
	}

	#endregion

	#region Private Types

	private sealed class InProcServerHost : IServerHost
	{
		public bool IsReady { get; private set; } = true;

		public int? ExitCode { get; private set; }

		public void Exit(int? exitCode)
		{
			this.IsReady = false;
			this.ExitCode = exitCode;
		}
	}

	#endregion
}
