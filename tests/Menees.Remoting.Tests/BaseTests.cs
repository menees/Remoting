namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#endregion

[TestClass]
public class BaseTests
{
	#region Private Data Members

	private LogManager? logManager;

	#endregion

	#region Public Properties

	public static bool IsDotNetFramework { get; } = RuntimeInformation.FrameworkDescription.Contains("Framework");

	public ILoggerFactory LoggerFactory => this.logManager?.LoggerFactory ?? NullLoggerFactory.Instance;

	#endregion

	#region Public Initialize/Cleanup Methods

	[TestInitialize]
	public void Initialize()
	{
		this.logManager = new();
	}

	[TestCleanup]
	public void Cleanup()
	{
		this.logManager?.Dispose();
		this.logManager = null;
	}

	#endregion

	#region Protected Methods

	protected static Task TestCrossProcessClientAsync(int clientCount, string serverPathPrefix, Scenario scenario, int iterations)
	{
		TimeSpan timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : ClientSettings.DefaultConnectTimeout;
		Parallel.ForEach(
			Enumerable.Range(1, clientCount),
			new ParallelOptions { MaxDegreeOfParallelism = Math.Min(clientCount, 8 * Environment.ProcessorCount) },
			item =>
			{
				InitializeProcessStartInfo(typeof(TestClient.Program), out ProcessStartInfo startInfo, out List<object> arguments);
				arguments.Add(scenario);
				arguments.Add(serverPathPrefix);
				arguments.Add(timeout);
				arguments.Add(iterations);
				FinalizeProcessStartInfo(startInfo, arguments);

				using Process clientProcess = new();
				clientProcess.StartInfo = startInfo;
				clientProcess.Start().ShouldBeTrue();
				TimeSpan exitWait = TimeSpan.FromSeconds(30);
				WaitForExit(clientProcess, exitWait, 0);
			});

		return Task.CompletedTask;
	}

	protected string GenerateServerPath([CallerMemberName] string? callerMemberName = null)
	{
		if (callerMemberName == null)
		{
			throw new ArgumentNullException(nameof(callerMemberName));
		}

		string result = $"{this.GetType().FullName}.{callerMemberName}";
		return result;
	}

	protected string GenerateServerPathPrefix([CallerMemberName] string? callerMemberName = null)
		=> this.GenerateServerPath(callerMemberName) + ".";

	protected async Task TestCrossProcessServerAsync(
		string serverPathPrefix,
		Func<string, Task> testClientAsync,
		int maxListeners,
		int minListeners = 1,
		Type? rmiServiceType = null)
	{
		InitializeProcessStartInfo(typeof(TestHost.Program), out ProcessStartInfo startInfo, out List<object> arguments);
		rmiServiceType ??= typeof(Tester);
		arguments.Add(rmiServiceType.Assembly.Location);
		arguments.Add(rmiServiceType.FullName!);
		arguments.Add(serverPathPrefix);
		arguments.Add(maxListeners);
		arguments.Add(minListeners);
		FinalizeProcessStartInfo(startInfo, arguments);

		const int ExpectedExitCode = 12345;
		using Process hostProcess = new();
		hostProcess.StartInfo = startInfo;
		hostProcess.Start().ShouldBeTrue();
		try
		{
			Thread.Sleep(2000);
			hostProcess.HasExited.ShouldBeFalse();

			TimeSpan connectTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(2);
			string hostServerPath = $"{serverPathPrefix}{nameof(IServerHost)}";
			using RmiClient<IServerHost> hostClient = new(hostServerPath, connectTimeout: connectTimeout, loggerFactory: this.LoggerFactory);
			IServerHost serverHost = hostClient.CreateProxy();
			serverHost.IsReady.ShouldBeTrue();

			await testClientAsync(serverPathPrefix).ConfigureAwait(false);

			serverHost.Exit(ExpectedExitCode);
		}
		finally
		{
			TimeSpan exitWait = TimeSpan.FromSeconds(10);
			WaitForExit(hostProcess, exitWait, ExpectedExitCode);
		}
	}

	#endregion

	#region Private Protected Methods

	private protected static void TestProxy(ITester testerProxy, int testId, bool isSingleClient)
	{
		testerProxy.TestId = testId;
		int actualTestId = testerProxy.TestId;

		// With multiple simultaneous clients, we can't guarantee that the value returned from the property
		// will be what we pushed in because another thread/client could have changed it.
		if (isSingleClient)
		{
			actualTestId.ShouldBe(testId);
		}

		testerProxy.Combine("A", "B").ShouldBe("AB");
		testerProxy.Combine("A", "B", "C").ShouldBe("ABC");

		Widget paper = testerProxy.CreateWidget("Paper", 0.01m, 85, 110);
		paper.Name.ShouldBe("Paper");
		paper.Cost.ShouldBe(0.01m);
		paper.Dimensions.ShouldBe([85, 110]);

		paper = testerProxy.UpdateWidget(paper, "Fancy Paper", 0.02m, null);
		paper.Name.ShouldBe("Fancy Paper");
		paper.Cost.ShouldBe(0.02m);
		paper.Dimensions.ShouldBe([85, 110]);
	}

	private protected static void WriteUnhandledServerException(Exception ex)
		=> Console.WriteLine("ERROR: Unhandled server exception: " + ex);

	#endregion

	#region Private Methods

	private static void InitializeProcessStartInfo(Type hostProgram, out ProcessStartInfo startInfo, out List<object> arguments)
	{
		string hostExeLocation = hostProgram.Assembly.Location;

		startInfo = new()
		{
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
			ErrorDialog = false,
		};

		arguments = [];
		if (string.Equals(Path.GetExtension(hostExeLocation), ".exe", StringComparison.OrdinalIgnoreCase))
		{
			startInfo.FileName = Path.GetFileName(hostExeLocation);
		}
		else
		{
			startInfo.FileName = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet\dotnet.exe");
			arguments.Add(hostExeLocation);
		}
	}

	private static void FinalizeProcessStartInfo(ProcessStartInfo startInfo, List<object> arguments)
	{
		startInfo.Arguments = string.Join(" ", arguments.Select(arg => $"\"{arg}\""));
	}

	private static void WaitForExit(Process process, TimeSpan exitWait, int expectedExitCode, [CallerMemberName] string? caller = null)
	{
		if (process.WaitForExit((int)exitWait.TotalMilliseconds))
		{
			process.WaitForExit(); // Let console finish flushing.
			process.ExitCode.ShouldBe(expectedExitCode);
		}
		else
		{
			process.Kill();
			Assert.Fail($"{caller} process didn't exit within wait time of {exitWait}.");
		}
	}

	#endregion
}
