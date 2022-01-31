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
	public void CloneStringTest()
	{
		string serverPath = typeof(string).FullName!;
		string expected = Guid.NewGuid().ToString();
		using RmiServer<ICloneable> server = new(serverPath, expected, loggerFactory: this.Loggers);

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
		this.TestCombine(1, 1, 20);
	}

	[TestMethod]
	public void MultiServerMedium()
	{
		// Run 100 clients that will have to wait on the 4-20 server listeners.
		this.TestCombine(4, 20, 100);
	}

	[TestMethod]
	public void MultiServerLarge()
	{
		// Run 5000 clients that will have to wait on the 20-100 server listeners.
		this.TestCombine(20, 100, 5000);
	}

	[TestMethod]
	public void UnlimitedServerMedium()
	{
		// Run 500 clients that will have to wait on the available server listeners.
		this.TestCombine(1, RmiServer<ITester>.MaxAllowedListeners, 500);
	}

	[TestMethod]
	public void InProcessServer()
	{
		const string serverPath = nameof(this.InProcessServer);
		InProcServerHost host = new();
		using RmiServer<IServerHost> server = new(serverPath, host, 2, 2, loggerFactory: this.Loggers);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<IServerHost> client = new(serverPath, loggerFactory: this.Loggers);
		IServerHost proxy = client.CreateProxy();
		host.IsShutdown.ShouldBeFalse();
		proxy.Shutdown();
		host.IsShutdown.ShouldBeTrue();
	}

	[TestMethod]
	public void CrossProcessServer()
	{
		// TODO: Launch ServerHost process with ServerHostManager service. [Bill, 1/29/2022]
	}

	#endregion

	#region Internal Methods

	internal static void WriteUnhandledServerException(Exception ex)
		=> Console.WriteLine("ERROR: Unhandled server exception: " + ex);

	#endregion

	#region Private Methods

	private void TestCombine(
		int minServerListeners,
		int maxServerListeners,
		int clientCount,
		[CallerMemberName] string? callerMemberName = null)
	{
		string serverPath = callerMemberName ?? throw new ArgumentNullException(nameof(callerMemberName));

		Tester tester = new();
		using RmiServer<ITester> server = new(serverPath, tester, maxServerListeners, minServerListeners, loggerFactory: this.Loggers);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		this.TestCombine(clientCount, serverPath);
	}

	private void TestCombine(int clientCount, string serverPath)
	{
		TimeSpan timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : RmiClient<ITester>.DefaultConnectTimeout;
		Parallel.ForEach(
			Enumerable.Range(1, clientCount),
			new ParallelOptions { MaxDegreeOfParallelism = Math.Min(clientCount, 8 * Environment.ProcessorCount) },
			item =>
			{
				using RmiClient<ITester> client = new(serverPath, connectTimeout: timeout, loggerFactory: this.Loggers);
				ITester proxy = client.CreateProxy();

				const string Prefix = "Item";
				string actual = proxy.Combine(Prefix, item.ToString());
				actual.ShouldBe(Prefix + item);
			});
	}

	#endregion

	#region Private Types

	private sealed class InProcServerHost : IServerHost
	{
		public bool IsShutdown { get; private set; }

		public void Shutdown()
		{
			this.IsShutdown = true;
		}
	}

	#endregion
}
