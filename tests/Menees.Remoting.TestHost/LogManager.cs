namespace Menees.Remoting.TestHost;

#region Using Directives

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;

#endregion

public sealed class LogManager : IDisposable
{
	#region Private Data Members

	private ILoggerFactory? loggerFactory;

	#endregion

	#region Constructors

	public LogManager(LogLevel minimumLogLevel = LogLevel.Debug)
	{
		this.loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();

			builder.SetMinimumLevel(minimumLogLevel);

			// Make the messages show up in the debugger's Output window.
			builder.AddDebug();

			// The Microsoft.Extensions.Logging.Console provider behaves weird in multi-threaded unit tests due to buffering.
			// So we'll dispose the whole loggerFactory after each test to try to force everything to flush out then.
			// https://codeburst.io/unit-testing-with-net-core-ilogger-t-e8c16c503a80
			builder.AddSimpleConsole(options =>
			{
				options.IncludeScopes = true;
				options.TimestampFormat = "HH:mm:ss.fff ";
			});
		});
	}

	#endregion

	#region Public Properties

	public ILoggerFactory LoggerFactory => this.loggerFactory ?? NullLoggerFactory.Instance;

	#endregion

	#region Public Initialize/Cleanup Methods

	public void Dispose()
	{
		this.loggerFactory?.Dispose();
		this.loggerFactory = null;
	}

	#endregion
}
