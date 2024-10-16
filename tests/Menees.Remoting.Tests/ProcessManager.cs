namespace Menees.Remoting;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

#endregion

internal sealed class ProcessManager
{
	#region Private Data Members

	private readonly List<object> arguments = [];
	private readonly List<string> output = [];
	private readonly List<string> error = [];

	#endregion

	#region Constructors

	public ProcessManager(Type hostProgram)
	{
		string hostExeLocation = hostProgram.Assembly.Location;

		this.StartInfo = new()
		{
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
			ErrorDialog = false,
		};

		if (string.Equals(Path.GetExtension(hostExeLocation), ".exe", StringComparison.OrdinalIgnoreCase))
		{
			this.StartInfo.FileName = Path.GetFileName(hostExeLocation);
		}
		else
		{
			this.StartInfo.FileName = OperatingSystem.IsWindows()
				? Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet\dotnet.exe")
				: "dotnet";
			this.arguments.Add(hostExeLocation);
		}
	}

	#endregion

	#region Public Properties

	public ProcessStartInfo StartInfo { get; }

	#endregion

	#region Public Methods

	public void Add(object argument) => this.arguments.Add(argument);

	public Process Start(bool redirectStreams = true)
	{
		this.StartInfo.Arguments = string.Join(" ", this.arguments.Select(arg => $"\"{arg}\""));
		this.StartInfo.RedirectStandardOutput = redirectStreams;
		this.StartInfo.RedirectStandardError = redirectStreams;

		Process result = new() { StartInfo = this.StartInfo };
		if (redirectStreams)
		{
			result.OutputDataReceived += (s, e) =>
			{
				lock (this.output)
				{
					this.output.Add(e.Data ?? string.Empty);
				}
			};

			result.ErrorDataReceived += (s, e) =>
			{
				lock (this.error)
				{
					this.error.Add(e.Data ?? string.Empty);
				}
			};
		}

		result.Start().ShouldBeTrue();

		if (redirectStreams)
		{
			result.BeginOutputReadLine();
			result.BeginErrorReadLine();
		}

		return result;
	}

	public void WaitForExit(Process process, TimeSpan exitWait, int expectedExitCode, [CallerMemberName] string? caller = null)
	{
		if (process.WaitForExit((int)exitWait.TotalMilliseconds))
		{
			process.WaitForExit(); // Let console finish flushing.
			this.WriteStreams();
			process.ExitCode.ShouldBe(expectedExitCode);
		}
		else
		{
			this.WriteStreams();
			process.Kill();
			Assert.Fail($"{caller} process didn't exit within wait time of {exitWait}.");
		}
	}

	#endregion

	#region Private Methods

	private void WriteStreams()
	{
		string output = string.Join(Environment.NewLine, this.output);
		string error = string.Join(Environment.NewLine, this.error);
		Debug.WriteLine(output);
		Debug.WriteLine(error);
	}

	#endregion
}
