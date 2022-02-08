namespace Menees.Remoting;

#region Using Directives

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

#endregion

[TestClass]
public class MessageNodeTests : BaseTests
{
	#region Public Methods

	[TestMethod]
	public async Task CodeNameToStringInProcessAsync()
	{
		const string ServerPath = nameof(this.CodeNameToStringInProcessAsync);

		using MessageServer<CodeName, string> server = new(
			async codeName => await Task.FromResult($"{codeName.Code}: {codeName.Name}").ConfigureAwait(false),
			ServerPath,
			loggerFactory: this.Loggers);
		server.Start();

		using MessageClient<CodeName, string> client = new(ServerPath, loggerFactory: this.Loggers);
		string response = await client.SendAsync(new CodeName { Code = 1, Name = "Billy" }).ConfigureAwait(false);
		response.ShouldBe("1: Billy");
		response = await client.SendAsync(new CodeName { Code = 2, Name = "Bob" }).ConfigureAwait(false);
		response.ShouldBe("2: Bob");
	}

	[TestMethod]
	public async Task StringToCodeNameInProcessAsync()
	{
		const string ServerPath = nameof(this.StringToCodeNameInProcessAsync);

		using MessageServer<string, CodeName> server = new(
			async text => await Task.FromResult(new CodeName { Code = text.Length, Name = text }).ConfigureAwait(false),
			ServerPath,
			maxListeners: 10,
			loggerFactory: this.Loggers);
		server.Start();

		using MessageClient<string, CodeName> client = new(ServerPath, loggerFactory: this.Loggers);
		IEnumerable<Task> tasks = Enumerable.Range(1, 20).Select(async item =>
		{
			string combined = "Item " + item;
			CodeName response = await client.SendAsync(combined).ConfigureAwait(false);
			response.Code.ShouldBe(combined.Length);
			response.Name.ShouldBe(combined);
		});
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[TestMethod]
	public void CrossProcess()
	{
		// TODO: Finish CrossProcess. [Bill, 2/7/2022]
	}

	[TestMethod]
	public void Cancellation()
	{
		// TODO: Finish Cancellation. [Bill, 2/8/2022]
	}

	[TestMethod]
	public async Task ThrowExceptionAsync()
	{
		const string ServerPath = nameof(this.ThrowExceptionAsync);

		const string ExceptionMessage = "Only even numbers are supported.";
		using MessageServer<int, string> server = new(
			async value =>
			{
				if (value % 2 == 1)
				{
					throw new ArgumentException(ExceptionMessage);
				}

				return await Task.FromResult(value.ToString()).ConfigureAwait(false);
			},
			ServerPath,
			loggerFactory: this.Loggers);
		server.Start();

		using MessageClient<int, string> client = new(ServerPath, loggerFactory: this.Loggers);
		for (int i = 0; i < 2; i++)
		{
			try
			{
				string response = await client.SendAsync(i).ConfigureAwait(false);
				i.ShouldBe(0);
				response.ShouldBe(i.ToString());
			}
			catch (ArgumentException ex)
			{
				i.ShouldBe(1);
				ex.Message.ShouldBe(ExceptionMessage);
			}
		}
	}

	#endregion

	#region Private Types

	private sealed class CodeName
	{
		#region Public Properties

		public int Code { get; set; }

		public string Name { get; set; } = string.Empty;

		#endregion
	}

	#endregion
}
