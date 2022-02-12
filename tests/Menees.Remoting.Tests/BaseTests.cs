namespace Menees.Remoting;

#region Using Directives

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

	#endregion
}
