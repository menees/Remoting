namespace Menees.Remoting;

#region Using Directives

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Collections;
using System.IO;

#endregion

[TestClass]
public class BaseTests
{
	#region Private Data Members

	private ILoggerFactory? loggerFactory;

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

		this.loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();

			builder.SetMinimumLevel(MinimumLogLevel);

			// Make the messages show up in the debugger's Output window.
			builder.AddDebug();

			// Note: We can't use any of the AddConsole methods from Microsoft.Extensions.Logging.Console because
			// they all buffer the formatted lines in a worker queue, and the lines won't show up in the correct unit test
			// due to the way MSTest attaches and detaches from stdout and stderr for each test.
			// https://codeburst.io/unit-testing-with-net-core-ilogger-t-e8c16c503a80
			// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/ConsoleLogger.cs#L61
			//
			// One workaround is to (re)create the logger factory and dispose of it in every test, but that's tedious.
			builder.AddConsoleFormatter<CustomConsoleFormatter, ConsoleFormatterOptions>(options =>
			{
				options.IncludeScopes = true;
				options.TimestampFormat = "HH:mm:ss.fff ";
			});

			builder.AddConsole(options => options.FormatterName = nameof(CustomConsoleFormatter));
		});
	}

	[TestCleanup]
	public void Cleanup()
	{
		this.loggerFactory?.Dispose();
		this.loggerFactory = null;
	}

	#endregion

	#region Private Types

	private sealed class CustomConsoleFormatter : ConsoleFormatter
	{
		#region Private Data Members

		private readonly ConsoleFormatterOptions options;

		#endregion

		#region Constructors

		public CustomConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
			: base(nameof(CustomConsoleFormatter))
		{
			this.options = options.CurrentValue;
		}

		#endregion

		#region Public Methods

		public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
		{
			StringBuilder sb = new();
			DateTime time = this.options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
			sb.Append('[').Append(time.ToString(this.options.TimestampFormat ?? "HH:mm:ss "));
			string levelName = logEntry.LogLevel switch
			{
				LogLevel.Trace => "TRC",
				LogLevel.Debug => "DBG",
				LogLevel.Information => "INF",
				LogLevel.Warning => "WRN",
				LogLevel.Error => "ERR",
				LogLevel.Critical => "CRT",
				LogLevel.None => "NON",
				_ => logEntry.LogLevel.ToString(),
			};
			sb.Append(levelName).Append("] ");

			if (logEntry.EventId.Id != 0)
			{
				sb.Append('#').Append(logEntry.EventId.Id).Append(' ');
			}

			sb.Append(logEntry.Category).Append(' ');

			string message = logEntry.Formatter?.Invoke(logEntry.State, null) ?? string.Empty;
			sb.Append(message);
			if (logEntry.Exception != null)
			{
				sb.Append(' ');
				Append(sb, logEntry.Exception, tag: "Exception: ");
			}

			if (this.options.IncludeScopes)
			{
				scopeProvider.ForEachScope(
					(scope, stringBuilder) =>
					{
						stringBuilder.Append(' ');
						Append(stringBuilder, scope);
					},
					sb);
			}

			string line = sb.ToString();
			textWriter.WriteLine(line);
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
	}

	#endregion
}
