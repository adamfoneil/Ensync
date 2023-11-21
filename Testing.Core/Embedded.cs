using Ensync.Core;
using Ensync.Core.Abstract;
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
	public async Task UnexpectedUniqueDrop() => await TestEmbeddedAsync("UnexpectedUniqueDrop.zip",
	[
		"ALTER TABLE [dbo].[WidgetType] ALTER COLUMN [Name] nvarchar(50) NOT NULL",
		"ALTER TABLE [dbo].[WidgetType] ADD CONSTRAINT [U_WidgetType_Name] UNIQUE ([Name] ASC)",
		"ALTER TABLE [dbo].[Widget] ADD CONSTRAINT [FK_Widget_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[WidgetType] ([Id])"
	]);

	[TestMethod]
	public async Task ShouldRebuildFK() => await TestEmbeddedAsync("ShouldRebuildFK.zip",
	[
		"ALTER TABLE [dbo].[WidgetType] ADD [ParentId] int NOT NULL",
		"ALTER TABLE [dbo].[WidgetType] ADD CONSTRAINT [U_WidgetType_Name_ParentId] UNIQUE ([Name] ASC, [ParentId] ASC)",
		"ALTER TABLE [dbo].[WidgetType] DROP CONSTRAINT [U_WidgetType_Name]"
	]);

	[TestMethod]
	public async Task RemoveImplicitFKDrops() => await TestEmbeddedAsync("RemoveImplicitFKDrops.zip",
	[
		"CREATE TABLE [dbo].[Customer] (\r\n   [Id] int identity(1,1) NOT NULL,\r\n   [Name] nvarchar(100) NULL\r\n)",
		"ALTER TABLE [dbo].[Customer] ADD CONSTRAINT [PK_Customer] PRIMARY KEY ([Id] ASC)",
		"DROP TABLE [dbo].[Widget]",
		"DROP TABLE [dbo].[WidgetType]"
	]);

	[TestMethod]
	public async Task ShouldAddConventionColumns() => await TestEmbeddedAsync("ShouldAddConventionColumns.zip",
	[
		"ALTER TABLE [dbo].[Customer] ADD [CreatedAt] datetime NOT NULL",
		"ALTER TABLE [dbo].[OrderHeader] ADD [CreatedAt] datetime NOT NULL",
		"ALTER TABLE [dbo].[Item] ADD [CreatedAt] datetime NOT NULL",
		"ALTER TABLE [dbo].[OrderLine] ADD [CreatedAt] datetime NOT NULL"
	]);

	[TestMethod]
	public async Task CalculatedColumnOrder() => await TestEmbeddedAsync("CalculatedColumnOrder.zip",
	[
		"ALTER TABLE [dbo].[OrderLine] ADD [SalesTax] money NOT NULL",
		"ALTER TABLE [dbo].[OrderLine] DROP COLUMN [ExtendedPrice]",
		"ALTER TABLE [dbo].[OrderLine] ADD [ExtendedPrice] AS ([Quantity] * [Price]) \u002B [SalesTax] PERSISTED"		
	]);

	private static async Task TestEmbeddedAsync(string resourceName, IEnumerable<string> shouldGenerateStatements)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Testing.Core.EmbeddedCases." + resourceName) ?? throw new Exception($"Resource not found: {resourceName}");
		using var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

		var source = GetEntryData<Schema>("source.json");
		SetFKParents(source);
		var target = GetEntryData<Schema>("target.json");
		SetFKParents(target);
		var connectionString = GetEntryData<string>("connection.json");
		var metadata = GetEntryData<DatabaseMetadata>("metadata.json");

		var scriptBuilder = new SqlServerScriptBuilder(connectionString);
		scriptBuilder.SetMetadata(metadata);
		var script = await source.CompareAsync(target, scriptBuilder);
		var statements = script.ToSqlStatements(scriptBuilder, true);

		Assert.IsTrue(statements.SequenceEqual(shouldGenerateStatements));

		T GetEntryData<T>(string entryName)
		{
			var entry = zipFile!.GetEntry(entryName) ?? throw new Exception($"Entry not found: {entryName}");
			var json = new StreamReader(entry.Open()).ReadToEnd();
			return JsonSerializer.Deserialize<T>(json) ?? throw new Exception("Couldn't read json");
		}

		void SetFKParents(Schema schema)
		{
			foreach (var fk in schema.ForeignKeys)
			{
				fk.Parent = schema.TableDictionary[fk.ParentName!];
			}
		}
	}
}
