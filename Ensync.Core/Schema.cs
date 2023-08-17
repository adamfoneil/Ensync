using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;
using Ensync.Core.Extensions;
using System.Text.Json.Serialization;
using Index = Ensync.Core.DbObjects.Index;

namespace Ensync.Core;

public class Schema
{
	public IEnumerable<Table> Tables { get; set; } = Enumerable.Empty<Table>();
	public IEnumerable<ForeignKey> ForeignKeys { get; set; } = Enumerable.Empty<ForeignKey>();

	[JsonIgnore]
	public Dictionary<string, Table> TableDictionary => Tables.ToDictionary(tbl => tbl.Name);

	public async Task<IEnumerable<ScriptAction>> CompareAsync(Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug = false)
	{
		ArgumentNullException.ThrowIfNull(nameof(targetSchema));
		ArgumentNullException.ThrowIfNull(nameof(scriptBuilder));

		await scriptBuilder.InspectTargetDatabaseAsync();

		SetParents(this);
		SetParents(targetSchema);

		List<ScriptAction> results = new();

        AlterColumns(results, Tables, targetSchema, scriptBuilder, debug);
        // AlterIndexes
        // AlterChecks
        // AlterForeignKeys

        AddTables(results, Tables, targetSchema, scriptBuilder, debug);
		AddColumns(results, Tables, targetSchema, scriptBuilder, debug);
		AddIndexes(results, Tables, targetSchema, scriptBuilder, debug);
		// AddChecks
		AddForeignKeys(results, ForeignKeys, targetSchema, scriptBuilder, debug);

		DropTables(results, Tables, targetSchema, scriptBuilder, debug);
		DropColumns(results, Tables, targetSchema, scriptBuilder, debug);
		DropIndexes(results, Tables, targetSchema, scriptBuilder, debug);
		DropForeignKeys(results, ForeignKeys, targetSchema, scriptBuilder, debug);

		return results;
	}

	private static void DropForeignKeys(List<ScriptAction> results, IEnumerable<ForeignKey> foreignKeys, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		results.AddRange(targetSchema.ForeignKeys.Except(foreignKeys)
			.Where(scriptBuilder.TargetObjectExists)
			.Select(fk => new ScriptAction(ScriptActionType.Drop, fk)
		{
			Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, fk.Parent, fk)
		}));
	}

	private static void AlterColumns(List<ScriptAction> results, IEnumerable<Table> tables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		var commonTables = GetCommonTables(tables, targetSchema.Tables);
		var commonColumns = commonTables.SelectMany(GetCommonColumns);

		results.AddRange(commonColumns.Where(IsAltered).Select(colPair => new ScriptAction(ScriptActionType.Alter, colPair.Source)
		{
			Statements = scriptBuilder.GetScript(ScriptActionType.Alter, targetSchema, colPair.Source.Parent, colPair.Source)
		}));
	}

	private static bool IsAltered((Column Source, Column Target) columnPair) => columnPair.Source.IsAltered(columnPair.Target).Result;

	private static IEnumerable<(Column Source, Column Target)> GetCommonColumns((Table Source, Table Target) tablePair) =>
		tablePair.Source.Columns.Join(
			tablePair.Target.Columns,
			col => col, col => col,
			(sourceCol, targetCol) => (sourceCol, targetCol));

	public async Task<IEnumerable<ScriptAction>> CreateAsync(SqlScriptBuilder scriptBuilder, bool debug = false)
	{
		var target = new Schema();
		return await CompareAsync(target, scriptBuilder, debug);
	}

	public async Task<string> CreateScriptAsync(SqlScriptBuilder scriptBuilder, string separator, bool debug = false)
	{
		var script = await CreateAsync(scriptBuilder, debug);
		return script.ToSqlScript(separator, scriptBuilder);
	}

	/// <summary>
	/// certain script syntax requires access to the object parent 
	/// (e.g. indexes have an "on" clause that references the parent table explicitly).
	/// object Parent properties aren't set necessarily in the original graph, so they're set here
	/// </summary>    
	private static void SetParents(Schema schema)
	{
		foreach (var table in schema.Tables)
		{
			foreach (var col in table.Columns) col.Parent ??= table;
			foreach (var index in table.Indexes) index.Parent ??= table;
			foreach (var chk in table.CheckConstraints) chk.Parent ??= table;
		}
	}

	private static void AddIndexes(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);

		results.AddRange(commonTables.SelectMany(tblPair => tblPair.Source.Indexes.Except(tblPair.Target.Indexes))
			.Select(ndx => new ScriptAction(ScriptActionType.Create, ndx)
			{
				Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, ndx.Parent, ndx, debug),
			}));
	}

	private static void DropIndexes(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);
		var alreadyDroppedIndexes = results.Where(IsDrop).SelectMany(a => a.Statements.Select(st => st.AffectedObject).OfType<Index>());

		results.AddRange(commonTables
			.SelectMany(tblPair => tblPair.Target.Indexes.Except(tblPair.Source.Indexes.Concat(alreadyDroppedIndexes)))
			.Where(scriptBuilder.TargetObjectExists)
			.Select(ndx => new ScriptAction(ScriptActionType.Drop, ndx)
			{
				Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, ndx.Parent, ndx, debug)
			}));

		static bool IsDrop(ScriptAction action) => action.Action == ScriptActionType.Drop;
	}

	private static void AddForeignKeys(List<ScriptAction> results, IEnumerable<ForeignKey> foreignKeys, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		results.AddRange(foreignKeys
			.Where(ReferencedTableCreatedOrExists)
			.Except(targetSchema.ForeignKeys)
			.Select(fk => new ScriptAction(ScriptActionType.Create, fk)
			{
				Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, fk.Parent, fk, debug)
			}));

		bool ReferencedTableCreatedOrExists(ForeignKey key)
		{
			if (results.Any(sa => sa.Object.Equals(key.ReferencedTable))) return true;
			if (scriptBuilder.Metadata.TableNames.Contains(key.ReferencedTable.Name)) return true;

			return false;
		}
	}

	private static void AddTables(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		results.AddRange(sourceTables.Except(targetSchema.Tables).Select(tbl => new ScriptAction(ScriptActionType.Create, tbl)
		{
			Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, null, tbl, debug)
		}));
	}

	private static void DropTables(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		results.AddRange(targetSchema.Tables.Except(sourceTables)
			.Where(scriptBuilder.TargetObjectExists)
			.Select(tbl => new ScriptAction(ScriptActionType.Drop, tbl)
		{
			IsDestructive = scriptBuilder.Metadata.GetRowCount(tbl.Name) > 0,
			Message = scriptBuilder.Metadata.GetDropWarning(tbl.Name),
			Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, null, tbl, debug)
		}));
	}

	private static void AddColumns(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);

		results.AddRange(commonTables.SelectMany(tablePair => tablePair.Source.Columns.Except(tablePair.Target.Columns).Select(col => new ScriptAction(ScriptActionType.Create, col)
		{
			Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, tablePair.Source, col, debug)
		})));
	}

	private static void DropColumns(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder, bool debug)
	{
		var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);

		results.AddRange(commonTables.SelectMany(tablePair => tablePair.Target.Columns.Except(tablePair.Source.Columns).Select(col => new ScriptAction(ScriptActionType.Drop, col)
		{
			IsDestructive = scriptBuilder.Metadata.GetRowCount(tablePair.Target.Name) > 0,
			Message = scriptBuilder.Metadata.GetDropWarning(tablePair.Target.Name),
			Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, tablePair.Source, col, debug)
		})));
	}

	private static IEnumerable<(Table Source, Table Target)> GetCommonTables(IEnumerable<Table> sourceTables, IEnumerable<Table> targetTables) =>
		sourceTables.Join(targetTables, source => source, target => target, (source, target) => (source, target));
}
