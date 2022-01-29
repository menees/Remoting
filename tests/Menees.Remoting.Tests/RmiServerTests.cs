namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.Runtime.CompilerServices;

#endregion

[TestClass]
public class RmiServerTests
{
	#region Public Methods

	[TestMethod]
	public void CloneStringTest()
	{
		string serverPath = typeof(string).FullName!;
		string expected = Guid.NewGuid().ToString();
		using RmiServer<ICloneable> server = new(serverPath, expected);

		// This is a super weak, insecure example since it just checks for the word "System".
		server.TryGetType = typeName => typeName.Contains(nameof(System))
			? Type.GetType(typeName, true)
			: throw new ArgumentException("TryGetType disallowed " + typeName);

		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<ICloneable> client = new(serverPath);
		ICloneable proxy = client.CreateProxy();
		string actual = (string)proxy.Clone();
		actual.ShouldBe(expected);
	}

	[TestMethod]
	public void SingleServerMultiClient()
	{
		// Run 20 clients that have to wait on a single server listener.
		TestCombine(1, 1, 20);
	}

	[TestMethod]
	public void MultiServerMultiClientMedium()
	{
		// Run 100 clients that will have to wait on the 4-20 server listeners.
		TestCombine(4, 20, 100);
	}

	[TestMethod]
	public void MultiServerMultiClientLarge()
	{
		// Run 100 clients that will have to wait on the 4-20 server listeners.
		TestCombine(20, 100, 5000);
	}

	[TestMethod]
	public void InProcessServer()
	{
		const string serverPath = nameof(this.InProcessServer);
		InProcServerHost host = new();
		using RmiServer<IServerHost> server = new(serverPath, host, 2, 2);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<IServerHost> client = new(serverPath);
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

	private static void TestCombine(
		int minServerListeners,
		int maxServerListeners,
		int clientCount,
		[CallerMemberName] string? callerMemberName = null)
	{
		string serverPath = callerMemberName ?? throw new ArgumentNullException(nameof(callerMemberName));

		Tester tester = new();
		using RmiServer<ITester> server = new(serverPath, tester, maxServerListeners, minServerListeners);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		// TODO: Allow full clientCount after net48 hang is fixed. [Bill, 1/29/2022]
		TestCombine(clientCount / clientCount, serverPath);
	}

	private static void TestCombine(int clientCount, string serverPath)
	{
		TimeSpan timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : RmiClient<ITester>.DefaultConnectTimeout;
		Parallel.ForEach(
			Enumerable.Range(1, clientCount),
			new ParallelOptions { MaxDegreeOfParallelism = Math.Min(clientCount, 8 * Environment.ProcessorCount) },
			item =>
			{
				using RmiClient<ITester> client = new(serverPath, connectTimeout: timeout);
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
