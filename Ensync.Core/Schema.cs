using Ensync.Core.Abstract;
using Ensync.Core.Extensions;
using Ensync.Core.DbObjects;

using Index = Ensync.Core.DbObjects.Index;
using System.Linq;

namespace Ensync.Core;

public class Schema
{
    public IEnumerable<Table> Tables { get; set; } = Enumerable.Empty<Table>();

    public Dictionary<string, Table> TableDictionary => Tables.ToDictionary(tbl => tbl.Name);

    public IEnumerable<(Table Parent, ForeignKey ForeignKey)> ForeignKeys => Tables.SelectMany(tbl => tbl.ForeignKeys, (tbl, fk) => (tbl, fk));

    public async Task<IEnumerable<ScriptAction>> CompareAsync(Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        ArgumentNullException.ThrowIfNull(nameof(targetSchema));
        ArgumentNullException.ThrowIfNull(nameof(scriptBuilder));

        await scriptBuilder.InspectTargetDatabaseAsync();

        SetParents(this);
        SetParents(targetSchema);

        List<ScriptAction> results = new();

        AddTables(results, Tables, targetSchema, scriptBuilder);
        AddColumns(results, Tables, targetSchema, scriptBuilder);
        AddIndexes(results, Tables, targetSchema, scriptBuilder);
        // AddChecks
        AddForeignKeys(results, Tables, targetSchema, scriptBuilder);

        AlterColumns(results, Tables, targetSchema, scriptBuilder);
        // AlterIndexes
        // AlterChecks
        // AlterForeignKeys

        DropTables(results, Tables, targetSchema, scriptBuilder);
        DropColumns(results, Tables, targetSchema, scriptBuilder);
        DropIndexes(results, Tables, targetSchema, scriptBuilder);
        // DropForeignKeys

        return results;
    }

    private void AlterColumns(List<ScriptAction> results, IEnumerable<Table> tables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = GetCommonTables(tables, targetSchema.Tables);
        var commonColumns = commonTables.SelectMany(GetCommonColumns);

        results.AddRange(commonColumns.Where(IsAltered).Select(colPair => new ScriptAction(ScriptActionType.Alter, colPair.Source)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Alter, targetSchema, colPair.Source.Parent, colPair.Source)
        }));
    }

    private static bool IsAltered((Column Source, Column Target) columnPair) => columnPair.Source.IsAltered(columnPair.Target).Result;
    
    private IEnumerable<(Column Source, Column Target)> GetCommonColumns((Table Source, Table Target) tablePair) =>
        tablePair.Source.Columns.Join(
            tablePair.Target.Columns,
            col => col, col => col,
            (sourceCol, targetCol) => (sourceCol, targetCol));    

    public async Task<IEnumerable<ScriptAction>> CreateAsync(SqlScriptBuilder scriptBuilder)
    {
        var target = new Schema();
        return await CompareAsync(target, scriptBuilder);
    }

    public async Task<string> CreateScriptAsync(SqlScriptBuilder scriptBuilder, string separator)
    {
        var script = await CreateAsync(scriptBuilder);
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
            foreach (var fk in table.ForeignKeys) fk.Parent ??= table;
            foreach (var chk in table.CheckConstraints) chk.Parent ??= table;
        }
    }

    private static void AddIndexes(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);

        results.AddRange(commonTables.SelectMany(tblPair => tblPair.Source.Indexes.Except(tblPair.Target.Indexes))
            .Select(ndx => new ScriptAction(ScriptActionType.Create, ndx)
            {
                Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, ndx.Parent, ndx),
            }));
    }

    private static void DropIndexes(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);        
        var alreadyDroppedIndexes = results.Where(IsDrop).SelectMany(a => a.Statements.Select(st => st.AffectedObject).OfType<Index>());

        results.AddRange(commonTables.SelectMany(tblPair => tblPair.Target.Indexes.Except(tblPair.Source.Indexes.Concat(alreadyDroppedIndexes)))
            .Select(ndx => new ScriptAction(ScriptActionType.Drop, ndx)
            {
                Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, ndx.Parent, ndx)
            }));

        static bool IsDrop(ScriptAction action) => action.Action == ScriptActionType.Drop;
    }

    private void AddForeignKeys(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        results.AddRange(sourceTables.SelectMany(tbl => tbl.ForeignKeys
            .Where(ReferencedTableCreatedOrExists)
            .Except(TargetForeignKeys())
            .Select(fk => new ScriptAction(ScriptActionType.Create, fk)
            {
                Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, tbl, fk)
            })));

        bool ReferencedTableCreatedOrExists(ForeignKey key)
        {
            if (results.Any(sa => sa.Object.Equals(key.ReferencedTable))) return true;
            if (scriptBuilder.Metadata.Tables.Contains(key.ReferencedTable.Name)) return true;

            return false;
        }

        IEnumerable<ForeignKey> TargetForeignKeys() => targetSchema.Tables.SelectMany(tbl => tbl.ForeignKeys);
    }

    private static void AddTables(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        results.AddRange(sourceTables.Except(targetSchema.Tables).Select(tbl => new ScriptAction(ScriptActionType.Create, tbl)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, null, tbl)
        }));
    }

    private static void DropTables(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        results.AddRange(targetSchema.Tables.Except(sourceTables).Select(tbl => new ScriptAction(ScriptActionType.Drop, tbl)
        {
            IsDestructive = scriptBuilder.Metadata.GetRowCount(tbl.Name) > 0,
            Message = scriptBuilder.Metadata.GetDropWarning(tbl.Name),
            Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, null, tbl)
        }));
    }

    private static void AddColumns(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);

        results.AddRange(commonTables.SelectMany(tablePair => tablePair.Source.Columns.Except(tablePair.Target.Columns).Select(col => new ScriptAction(ScriptActionType.Create, col)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, tablePair.Source, col)
        })));
    }

    private static void DropColumns(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = GetCommonTables(sourceTables, targetSchema.Tables);

        results.AddRange(commonTables.SelectMany(tablePair => tablePair.Target.Columns.Except(tablePair.Source.Columns).Select(col => new ScriptAction(ScriptActionType.Drop, col)
        {
            IsDestructive = scriptBuilder.Metadata.GetRowCount(tablePair.Target.Name) > 0,
            Message = scriptBuilder.Metadata.GetDropWarning(tablePair.Target.Name),
            Statements = scriptBuilder.GetScript(ScriptActionType.Drop, targetSchema, tablePair.Source, col)
        })));
    }

    private static IEnumerable<(Table Source, Table Target)> GetCommonTables(IEnumerable<Table> sourceTables, IEnumerable<Table> targetTables) =>
        sourceTables.Join(targetTables, source => source, target => target, (source, target) => (source, target));
}
