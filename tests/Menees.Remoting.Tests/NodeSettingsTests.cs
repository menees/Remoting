namespace Menees.Remoting;

[TestClass]
public class NodeSettingsTests
{
	[TestMethod]
	public void RequireGetType()
	{
		const string CoreStringTypeName = "System.String, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";
		const string FrameworkStringTypeName = "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
		TestVersions(typeof(string), CoreStringTypeName, FrameworkStringTypeName);

		const string CoreDictionaryTypeName = "System.Collections.Generic.IReadOnlyDictionary`2[" +
			"[System.String, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]," +
			"[System.Object, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]" +
			"], System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";
		const string FrameworkDictionaryTypeName = "System.Collections.Generic.IReadOnlyDictionary`2[" +
			"[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]," +
			"[System.Object, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]" +
			"], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
		TestVersions(typeof(IReadOnlyDictionary<string, object>), CoreDictionaryTypeName, FrameworkDictionaryTypeName);

		Should.Throw<TypeLoadException>(() => NodeSettings.RequireGetType("This.Type.Does.Not.Exist"));

		static void TestVersions(Type expected, params string[] typeNames)
		{
			foreach (string typeName in typeNames)
			{
				// Make sure mixed runtimes work correctly for built-in types.
				NodeSettings.RequireGetType(typeName).ShouldBe(expected);

				// Make sure older and newer versions will resolve to the current version.
				NodeSettings.RequireGetType(AdjustVersion(typeName, 1)).ShouldBe(expected);
				NodeSettings.RequireGetType(AdjustVersion(typeName, 999)).ShouldBe(expected);
			}
		}

		static string AdjustVersion(string typeName, uint majorVersion)
		{
			string result = typeName;

			const string Prefix = ", Version=";
			int startIndex = 0;
			while ((startIndex = result.IndexOf(Prefix, startIndex)) >= 0)
			{
				int endIndex = result.IndexOf('.', startIndex);
				result = result.Substring(0, startIndex + Prefix.Length) + majorVersion + result.Substring(endIndex);
				startIndex = endIndex;
			}

			return result;
		}
	}
}