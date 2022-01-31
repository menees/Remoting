namespace Menees.Remoting;

#region Using Directives

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

#endregion

[TestClass]
public sealed class AssemblyEvents
{
	#region Private Data Members

	private static ILoggerFactory? loggerFactory;

	#endregion

	#region Public Initialize/Cleanup Methods

	[AssemblyInitialize]
	public static void Initialize(TestContext testContext)
	{
		// We have to reference testContext to make the compiler happy,
		// and we have to take it as a parameter for AssemblyInitialize to work.
		testContext.GetHashCode();

		loggerFactory = LoggerFactory.Create(builder =>
		{
			builder
			.SetMinimumLevel(LogLevel.Trace)
			.AddDebug() // Make the messages show up in the debugger's Output window.
			.AddSimpleConsole(options =>
			{
				options.ColorBehavior = LoggerColorBehavior.Disabled;
				options.IncludeScopes = true;
			});
		});
	}

	[AssemblyCleanup]
	public static void Cleanup()
	{
		loggerFactory?.Dispose();
	}

	#endregion

	#region Public Helper Methods

	public static ILogger<T> CreateLogger<T>()
		=> (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<T>();

	public static ILogger<RmiServer<T>> CreateServerLogger<T>()
		where T : class
		=> (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<RmiServer<T>>();

	#endregion
}
