namespace Menees.Remoting;

#region Using Directives

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;

#endregion

[TestClass]
public class BaseTests
{
	#region Private Data Members

	private ILoggerFactory? loggerFactory;
	private ImmediateConsoleLoggerProvider? consoleLoggerProvider;

	#endregion

	#region Public Properties

	public ILoggerFactory Loggers => this.loggerFactory ?? NullLoggerFactory.Instance;

	#endregion

	#region Public Initialize/Cleanup Methods

	[TestInitialize]
	public void Initialize()
	{
		// Set this to Trace to see everything logged. Console logging is slower than Debugger logging.
		const LogLevel MinimumLogLevel = LogLevel.Debug;
		const LogLevel ConsoleLogLevel = LogLevel.Debug;

		this.loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();

			builder.SetMinimumLevel(MinimumLogLevel);

			// Make the messages show up in the debugger's Output window.
			builder.AddDebug();

			// TODO: Do we still need a custom provider, or will a custom formatter do? [Bill, 1/31/2022]
			// Note: We can't use any of the AddConsole methods from Microsoft.Extensions.Logging.Console because
			// they all buffer the formatted lines in a worker queue, and the lines won't show up in the correct unit test
			// due to the way MSTest attaches and detaches from stdout and stderr for each test.
			// https://codeburst.io/unit-testing-with-net-core-ilogger-t-e8c16c503a80
			// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/ConsoleLogger.cs#L61
			//
			// One workaround is to (re)create the logger factory and dispose of it in every test, but that's tedious.
			// Another workaround is to use a better logging system like Serilog.Sinks.Console, but it's internal buffering
			// can still show output in the wrong test. Or we can just use a simple local provider like
			// ImmediateConsoleLoggerProvider that does no buffering. It's predictable but slow. :-(
			this.consoleLoggerProvider = new ImmediateConsoleLoggerProvider(ConsoleLogLevel);
			builder.AddProvider(this.consoleLoggerProvider);
		});
	}

	[TestCleanup]
	public void Cleanup()
	{
		this.loggerFactory?.Dispose();
		this.loggerFactory = null;
		this.consoleLoggerProvider?.Dispose();
		this.consoleLoggerProvider = null;
	}

	#endregion

	#region Private Types

	private sealed class ImmediateConsoleLoggerProvider : ILoggerProvider
	{
		#region Private Data Members

		private readonly LogLevel minLevel;

		#endregion

		#region Constructors

		public ImmediateConsoleLoggerProvider(LogLevel minLevel)
		{
			this.minLevel = minLevel;
		}

		#endregion

		#region Public Methods

		public ILogger CreateLogger(string categoryName) => new ImmediateConsoleLogger(categoryName, this.minLevel);

		public void Dispose()
		{
		}

		#endregion

		private sealed class ImmediateConsoleLogger : ILogger
		{
			#region Private Data Members

			private readonly string categoryName;
			private readonly LogLevel minLevel;
			private readonly ThreadLocal<Stack<object?>> threadScope = new(() => new());

			#endregion

			#region Constructors

			public ImmediateConsoleLogger(string categoryName, LogLevel minLevel)
			{
				this.categoryName = categoryName;
				this.minLevel = minLevel;
			}

			#endregion

			#region Public Methods

			public IDisposable BeginScope<TState>(TState state)
				=> new Scope(this.threadScope.Value!, state);

			public bool IsEnabled(LogLevel logLevel) => logLevel >= this.minLevel;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
			{
				string levelName = logLevel switch
				{
					LogLevel.Trace => "TRC",
					LogLevel.Debug => "DBG",
					LogLevel.Information => "INF",
					LogLevel.Warning => "WRN",
					LogLevel.Error => "ERR",
					LogLevel.Critical => "CRT",
					LogLevel.None => "NON",
					_ => logLevel.ToString(),
				};

				StringBuilder sb = new();
				sb.AppendFormat("[{0:HH:mm:ss.fff} {1}] ", DateTime.Now, levelName);
				if (eventId.Id != 0)
				{
					sb.Append('#').Append(eventId.Id).Append(' ');
				}

				sb.Append(this.categoryName).Append(' ');

				string message = formatter(state, null);
				sb.Append(message);
				if (exception != null)
				{
					sb.Append(' ');
					Append(sb, exception, tag: "Exception: ");
				}

				foreach (object? value in this.threadScope.Value!)
				{
					sb.Append(' ');
					Append(sb, value);
				}

				string line = sb.ToString();
				Console.WriteLine(line);
			}

			#endregion

			#region Private Methods

			private static void Append(StringBuilder sb, object? value, bool delimit = true, string? tag = null)
			{
				if (delimit)
				{
					sb.Append('{');
				}

				sb.Append(tag);

				switch (value)
				{
					case string text:
						sb.Append(text);
						break;

					case IDictionary dictionary:
						foreach (DictionaryEntry entry in dictionary)
						{
							Append(sb, entry.Key, delimit: entry.Key is not string);
							sb.Append('=');
							Append(sb, entry.Value, delimit: entry.Value is not string);
						}

						break;

					case IEnumerable enumerable:
						foreach (object? item in enumerable)
						{
							Append(sb, item);
						}

						break;

					case Exception ex:
						// Some exception messages end with newlines.
						sb.Append(ex.Message.TrimEnd());
						if (ex.HResult != 0)
						{
							sb.Append(' ').Append("HResult=0x").AppendFormat("{0:X2}", ex.HResult);
						}

						if (ex.InnerException != null)
						{
							sb.Append(' ');
							Append(sb, ex.InnerException);
						}

						break;

					default:
						sb.Append(value);
						break;
				}

				if (delimit)
				{
					sb.Append('}');
				}
			}

			#endregion

			#region Private Types

			private sealed class Scope : IDisposable
			{
				#region Private Data Members

				private readonly Stack<object?> scope;

				#endregion

				#region Constructors

				public Scope(Stack<object?> scope, object? state)
				{
					this.scope = scope;
					this.scope.Push(state);
				}

				#endregion

				#region Public Methods

				public void Dispose()
				{
					this.scope.Pop();
				}

				#endregion
			}

			#endregion
		}
	}

	#endregion
}
