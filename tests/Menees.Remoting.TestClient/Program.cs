namespace Menees.Remoting.TestClient;

#region Using Directives

using System.Reflection;
using Menees.Remoting.TestHost;
using static System.Console;

#endregion

public static class Program
{
	#region Private Enums

	private enum ExitCode
	{
		Default = 0,
		UnhandledException = 1,
		MissingArgs = 2,
	}

	#endregion

	#region Main Entry Point

#pragma warning disable CC0061 // Asynchronous method can be terminated with the 'Async' keyword. Entry point name must be Main.
	public static async Task<int> Main(string[] args)
#pragma warning restore CC0061 // Asynchronous method can be terminated with the 'Async' keyword.
	{
		ExitCode exitCode = ExitCode.Default;

		const int RequiredArgCount = 4;

		// Sometimes a lighter weight option is to use SysInternals PipeList utility from PowerShell
		// to see what pipes are open: .\pipelist.exe |select-string Menees
		// Debugger.Launch();
		if (args.Length != RequiredArgCount)
		{
			exitCode = FataError(ExitCode.MissingArgs, $"Usage: {nameof(TestClient)} Scenario ServerPathPrefix ConnectTimeout Iterations");
		}
		else
		{
			try
			{
				Scenario scenario = (Scenario)Enum.Parse(typeof(Scenario), args[0]);
				string serverPathPrefix = args[1];
				TimeSpan connectTimeout = TimeSpan.Parse(args[2]);
				int iterations = int.Parse(args[3]);

				using LogManager logManager = new();

				ClientSettings rmiClientSettings = new(serverPathPrefix + nameof(ICalculator))
				{
					ConnectTimeout = connectTimeout,
					LoggerFactory = logManager.Loggers,
				};
				using RmiClient<ICalculator> rmiClient = new(rmiClientSettings);

				ClientSettings messageClientSettings = new(serverPathPrefix + "Echo")
				{
					ConnectTimeout = connectTimeout,
					LoggerFactory = logManager.Loggers,
				};
				using MessageClient<string, string> echoMessageClient = new(messageClientSettings);

				switch (scenario)
				{
					case Scenario.Calculator:
						ICalculator proxy = rmiClient.CreateProxy();
						DateTime dateTime = DateTime.Now;
						for (int i = 0; i < iterations; i++)
						{
							proxy.Add(1m, i).ShouldBe(1m + i);

							TimeSpan timeSpan = TimeSpan.FromHours(i);
							proxy.Add(dateTime, timeSpan).ShouldBe(dateTime + timeSpan);
						}

						break;

					case Scenario.Message:
						for (int i = 0; i < iterations; i++)
						{
							string request = "Test" + i;
							string response = await echoMessageClient.SendAsync(request).ConfigureAwait(false);
							response.ShouldBe(request);
						}

						break;
				}
			}
			catch (Exception ex)
			{
				exitCode = FataError(ExitCode.UnhandledException, $"Unhandled exception: {ex}");
			}
		}

		return (int)exitCode;
	}

	#endregion

	#region Private Methods

	private static ExitCode FataError(ExitCode exitCode, string message)
	{
		Error.WriteLine(message);
		Error.WriteLine($"Exit code: {exitCode}");
		return exitCode;
	}

	#endregion
}