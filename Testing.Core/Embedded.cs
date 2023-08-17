using Ensync.Core;
using Ensync.Core.Extensions;
using Ensync.SqlServer;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace Testing.Core;

[TestClass]
public class Embedded
{
	[TestMethod]
	public async Task UnexpectedUniqueDrop() => await TestEmbeddedAsync("Testing.Core.EmbeddedCases.UnexpectedUniqueDrop.zip", new[]
	{
        "ALTER TABLE [dbo].[WidgetType] ALTER COLUMN [Name] nvarchar(50) NOT NULL",
        "ALTER TABLE [dbo].[WidgetType] ADD CONSTRAINT [U_WidgetType_Name] UNIQUE ([Name] ASC)"		
	});
	
	private async Task TestEmbeddedAsync(string resourceName, IEnumerable<string> shouldGenerateStatements)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ?? throw new Exception($"Resource not found: {resourceName}");
		using var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

		var source = GetSchema("source.json");
		var target = GetSchema("target.json");
		var connectionString = GetConnectionString();

		var scriptBuilder = new SqlServerScriptBuilder(connectionString);
		var script = await source.CompareAsync(target, scriptBuilder);
		var statements = script.ToSqlStatements(scriptBuilder, true);

		Assert.IsTrue(statements.SequenceEqual(shouldGenerateStatements));

		Schema GetSchema(string entryName)
		{
			var entry = zipFile!.GetEntry(entryName) ?? throw new Exception($"Entry not found: {entryName}");
			var json = new StreamReader(entry.Open()).ReadToEnd();
			return JsonSerializer.Deserialize<Schema>(json) ?? throw new Exception("Couldn't read json");
		}

		string GetConnectionString()
		{
			var entry = zipFile.GetEntry("connection.json") ?? throw new Exception("Entry connection.json not found");
			var json = new StreamReader(entry.Open()).ReadToEnd();
			return JsonSerializer.Deserialize<string>(json) ?? throw new Exception("Couldn't read connection string json");
		}
	}
}
