namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#endregion

[TestClass]
#pragma warning disable MSTEST0016 // Test class should have test method. This is used as a base class with init and cleanup.
public class BaseTests
#pragma warning restore MSTEST0016 // Test class should have test method
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
				ProcessManager processManager = new(typeof(TestClient.Program));
				processManager.Add(scenario);
				processManager.Add(serverPathPrefix);
				processManager.Add(timeout);
				processManager.Add(iterations);

				using Process clientProcess = processManager.Start();
				TimeSpan exitWait = TimeSpan.FromSeconds(30);
				processManager.WaitForExit(clientProcess, exitWait, 0);
			});

		return Task.CompletedTask;
	}

	protected string GenerateServerPath([CallerMemberName] string? callerMemberName = null)
	{
		ArgumentNullException.ThrowIfNull(callerMemberName);

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
		ProcessManager processManager = new(typeof(TestHost.Program));
		rmiServiceType ??= typeof(Tester);
		processManager.Add(rmiServiceType.Assembly.Location);
		processManager.Add(rmiServiceType.FullName!);
		processManager.Add(serverPathPrefix);
		processManager.Add(maxListeners);
		processManager.Add(minListeners);

		// Note: On Linux, exit codes must be in byte's range not int's! https://stackoverflow.com/a/51820986/1882616
		const int ExpectedExitCode = 123;
		using Process hostProcess = processManager.Start();
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
			processManager.WaitForExit(hostProcess, exitWait, ExpectedExitCode);
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
}
