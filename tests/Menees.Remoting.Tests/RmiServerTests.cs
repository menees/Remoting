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
		string serverPath = typeof(string).FullName!;
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
		const string serverPath = nameof(this.InProcessServer);
		InProcServerHost host = new();
		using RmiServer<IServerHost> server = new(host, serverPath, 2, 2, loggerFactory: this.Loggers);
		server.ReportUnhandledException = WriteUnhandledServerException;
		server.Start();

		using RmiClient<IServerHost> client = new(serverPath, loggerFactory: this.Loggers);
		IServerHost proxy = client.CreateProxy();
		host.IsReady.ShouldBeTrue();
		proxy.Shutdown();
		host.IsReady.ShouldBeFalse();
	}

	[TestMethod]
	public void CrossProcessServer()
	{
		string serverHostLocation = typeof(IServerHost).Assembly.Location;

		ProcessStartInfo startInfo = new();
		List<string> arguments = new();
		if (string.Equals(Path.GetExtension(serverHostLocation), ".exe", StringComparison.OrdinalIgnoreCase))
		{
			startInfo.FileName = Path.GetFileName(serverHostLocation);
		}
		else
		{
			startInfo.FileName = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet\dotnet.exe");
			arguments.Add(serverHostLocation);
		}

		string serverPathPrefix = $"{typeof(RmiServerTests).FullName}.{nameof(this.CrossProcessServer)}.";
		arguments.Add(typeof(Tester).Assembly.Location);
		arguments.Add(typeof(Tester).FullName!);
		arguments.Add(serverPathPrefix);
		arguments.Add("20"); // MaxListeners
		arguments.Add("4"); // MinListeners

		// The current debugger can't be used. VS pops up a dialog to launch a new instance.
		// Sometimes a lighter weight option is to use SysInternals PipeList utility from PowerShell
		// to see what pipes are open: .\pipelist.exe |select-string Menees
		bool debugServerHost = Debugger.IsAttached && Convert.ToBoolean(0);
		arguments.Add(debugServerHost.ToString()); // LaunchDebugger

		startInfo.CreateNoWindow = true;
		startInfo.WindowStyle = ProcessWindowStyle.Hidden;
		startInfo.Arguments = string.Join(" ", arguments.Select(arg => $"\"{arg}\""));
		startInfo.ErrorDialog = false;

		using Process hostProcess = new();
		hostProcess.StartInfo = startInfo;
		hostProcess.Start().ShouldBeTrue();
		try
		{
			Thread.Sleep(2000);
			hostProcess.HasExited.ShouldBeFalse();

			TimeSpan connectTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(2);
			string hostServerPath = $"{serverPathPrefix}{nameof(IServerHost)}";
			using RmiClient<IServerHost> hostClient = new(hostServerPath, connectTimeout: connectTimeout, loggerFactory: this.Loggers);
			IServerHost serverHost = hostClient.CreateProxy();
			serverHost.IsReady.ShouldBeTrue();

			this.TestClient(50, $"{serverPathPrefix}{nameof(ITester)}");

			serverHost.Shutdown();
		}
		finally
		{
			TimeSpan exitWait = TimeSpan.FromSeconds(5);
			if (hostProcess.WaitForExit((int)exitWait.TotalMilliseconds))
			{
				hostProcess.WaitForExit(); // Let console finish flushing.
			}
			else
			{
				hostProcess.Kill();
				Assert.Fail($"Host process didn't exit within wait time of {exitWait}.");
			}
		}
	}

	#endregion

	#region Internal Methods

	internal static void WriteUnhandledServerException(Exception ex)
		=> Console.WriteLine("ERROR: Unhandled server exception: " + ex);

	#endregion

	#region Private Methods

	private void TestClient(
		int minServerListeners,
		int maxServerListeners,
		int clientCount,
		[CallerMemberName] string? callerMemberName = null)
	{
		string serverPath = callerMemberName ?? throw new ArgumentNullException(nameof(callerMemberName));

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

		public void Shutdown()
		{
			this.IsReady = false;
		}
	}

	#endregion
}
