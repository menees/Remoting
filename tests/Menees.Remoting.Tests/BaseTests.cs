namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

	public ILoggerFactory Loggers => this.logManager?.Loggers ?? NullLoggerFactory.Instance;

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
		int minListeners = 1)
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

		arguments.Add(typeof(Tester).Assembly.Location);
		arguments.Add(typeof(Tester).FullName!);
		arguments.Add(serverPathPrefix);
		arguments.Add(maxListeners.ToString());
		arguments.Add(minListeners.ToString());

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

			await testClientAsync(serverPathPrefix).ConfigureAwait(false);

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
		paper.Dimensions.ShouldBe(new[] { 85, 110 });

		paper = testerProxy.UpdateWidget(paper, "Fancy Paper", 0.02m, null);
		paper.Name.ShouldBe("Fancy Paper");
		paper.Cost.ShouldBe(0.02m);
		paper.Dimensions.ShouldBe(new[] { 85, 110 });
	}

	private protected static void WriteUnhandledServerException(Exception ex)
		=> Console.WriteLine("ERROR: Unhandled server exception: " + ex);

	#endregion
}
