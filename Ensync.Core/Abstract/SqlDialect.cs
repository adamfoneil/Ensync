namespace Ensync.Core.Abstract;

public abstract class SqlDialect
{
    public abstract Dictionary<DbObjectType, SqlStatements> Syntax { get; }

    protected abstract string FormatName(DbObject dbObject);

    public class SqlStatements
    {
        public Func<DbObject, string>? Definition { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<string>> Create { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<string>> Alter { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<string>> Drop { get; init; }

        public IEnumerable<string> GetScript(ScriptActionType actionType, DbObject? parent, DbObject child) => actionType switch
        {
            ScriptActionType.Create => Create.Invoke(parent, child),
            ScriptActionType.Alter => Alter.Invoke(parent, child), // drop dependencies, alter object, re-create dependencies
            ScriptActionType.Drop => Drop.Invoke(parent, child), // drop dependencies, drop object
            _ => throw new NotSupportedException()
        };
    }
}
