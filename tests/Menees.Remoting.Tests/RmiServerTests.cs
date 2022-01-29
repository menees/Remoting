namespace Menees.Remoting;

#region Using Directives

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
		// TODO: Make this work with 20 clients that have to queue up and wait. [Bill, 1/29/2022]
		// TODO: Fix case where server finishes but doesn't start a new listener. [Bill, 1/29/2022]
		TestCombine(1, 1, 1);
	}

	[TestMethod]
	public void MultiServerMultiClient()
	{
		// TODO: Make this work with 100 clients that may have to queue up and wait. [Bill, 1/29/2022]
		TestCombine(4, 20, 4);
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
		// TODO: Finish CrossProcessServer. [Bill, 1/29/2022]
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

		TestCombine(clientCount, serverPath);
	}

	private static void TestCombine(int clientCount, string serverPath)
	{
		TimeSpan timeout = TimeSpan.FromSeconds(5);
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
