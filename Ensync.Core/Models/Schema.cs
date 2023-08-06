using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Schema
{
    public IEnumerable<Table> Tables { get; set; } = Enumerable.Empty<Table>();

    public async Task<IEnumerable<ScriptAction>> CompareAsync(Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {       
        ArgumentNullException.ThrowIfNull(nameof(targetSchema));
        ArgumentNullException.ThrowIfNull(nameof(scriptBuilder));

        await scriptBuilder.InspectTargetDatabaseAsync();

        List<ScriptAction> results = new();

        CreateTables(results, Tables, targetSchema, scriptBuilder);

        AddColumns(results, Tables, targetSchema, scriptBuilder);
        // AddIndexes
        // AddChecks
        AddForeignKeys(results, Tables, targetSchema, scriptBuilder);

        // AlterColumns
        // AlterIndexes
        // AlterChecks
        // AlterForeignKeys

        // DropTables
        // DropColumns
        // DropIndexes
        // DropForeignKeys

        return results;
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

    private void CreateTables(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        results.AddRange(sourceTables.Except(targetSchema.Tables).Select(tbl => new ScriptAction(ScriptActionType.Create, tbl)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, null, tbl)
        }));
    }

    private void AddColumns(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = sourceTables.Join(targetSchema.Tables, source => source, target => target, (source, target) => new
        {
            Source = source,
            Target = target
        });

        results.AddRange(commonTables.SelectMany(tablePair => tablePair.Source.Columns.Except(tablePair.Target.Columns).Select(col => new ScriptAction(ScriptActionType.Create, col)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, tablePair.Source, col)
        })));
    }
}
