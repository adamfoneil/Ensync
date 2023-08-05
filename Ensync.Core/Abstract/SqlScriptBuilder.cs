using Ensync.Core.Models;

namespace Ensync.Core.Abstract;

public enum StatementPlacement
{
    /// <summary>
    /// most statements are run immediately
    /// </summary>
    Immediate,
    /// <summary>
    /// but foreign keys need to be at the end of the script
    /// </summary>
    Deferred
}

public abstract class SqlScriptBuilder
{
    public abstract Dictionary<DbObjectType, SqlStatements> Syntax { get; }

    protected abstract string FormatName(DbObject dbObject);

    public class SqlStatements
    {
        public Func<DbObject, string>? Definition { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<(StatementPlacement, string)>> Create { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<(StatementPlacement, string)>> Alter { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<(StatementPlacement, string)>> Drop { get; init; }

        public IEnumerable<(StatementPlacement, string)> GetScript(ScriptActionType actionType, Schema schema, DbObject? parent, DbObject child) => actionType switch
        {
            ScriptActionType.Create => Create.Invoke(parent, child),
            ScriptActionType.Alter => Alter.Invoke(parent, child), // drop dependencies, alter object, re-create dependencies
            ScriptActionType.Drop => DropDependencies(schema, parent, child).Concat(Drop.Invoke(parent, child)), // drop dependencies, drop object
            _ => throw new NotSupportedException()
        };

        private IEnumerable<(StatementPlacement, string)> DropDependencies(Schema schema, DbObject? parent, DbObject child) =>
            child.GetDependencies(schema).SelectMany(obj => Drop(parent, obj));
    }
}
