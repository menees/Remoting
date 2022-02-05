namespace Menees.Remoting;

#region Using Directives

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;

#endregion

public sealed class LogManager : IDisposable
{
	#region Private Data Members

	private ILoggerFactory? loggerFactory;
	private ImmediateConsoleLoggerProvider? consoleLoggerProvider;

	#endregion

	#region Constructors

	public LogManager(LogLevel minimumLogLevel = LogLevel.Debug, LogLevel consoleLogLevel = LogLevel.Debug)
	{
		this.loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();

			builder.SetMinimumLevel(minimumLogLevel);

			// Make the messages show up in the debugger's Output window.
			builder.AddDebug();

			// The Microsoft.Extensions.Logging.Console provider behaves terribly in multi-threaded unit tests.
			// Even when recreating the ILoggerFactory for each test, it shares a single logging scope for all
			// ILogger<T> instances of the same type. So two PipeServer instances share the same logger instance
			// and the same scope instance! That means BeginScope calls pile up when N instances are active. :-(
			// https://codeburst.io/unit-testing-with-net-core-ilogger-t-e8c16c503a80
			// So I wrote my own ImmediateConsoleLoggerProvider that's aware of async call contexts.
			// But since it doesn't do any buffering, it is very slow on large tests!
			this.consoleLoggerProvider = new ImmediateConsoleLoggerProvider(consoleLogLevel);
			builder.AddProvider(this.consoleLoggerProvider);
		});
	}

	#endregion

	#region Public Properties

	public ILoggerFactory Loggers => this.loggerFactory ?? NullLoggerFactory.Instance;

	#endregion

	#region Public Initialize/Cleanup Methods

	public void Dispose()
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
			private readonly AsyncLocal<Stack<object?>> callContextStack = new();

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
			{
				this.callContextStack.Value ??= new();
				IDisposable result = new Scope(this.callContextStack.Value, state);
				return result;
			}

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

				foreach (object? value in this.callContextStack.Value!)
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

				private readonly object monitor = new();
				private Stack<object?>? scope;

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
					// Protect against simultaneous multi-threaded disposal (e.g., PipeServerListener.Dispose()).
					lock (this.monitor)
					{
						Stack<object?>? stack = this.scope;
						this.scope = null;

						if (stack?.Count > 0)
						{
							stack.Pop();
						}
					}
				}

				#endregion
			}

			#endregion
		}
	}

	#endregion
}
