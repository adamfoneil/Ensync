namespace Ensync.Core.Abstract;

public abstract class SqlDialect
{
    public abstract Dictionary<DbObjectType, SqlStatements> Syntax { get; }

    public class SqlStatements
    {
        public Func<DbObject, string>? Definition { get; init; }
        public required Func<DbObject, IEnumerable<string>> Create { get; init; }
        public required Func<DbObject, IEnumerable<string>> Alter { get; init; }
        public required Func<DbObject, IEnumerable<string>> Drop { get; init; }

        public IEnumerable<string> GetScript(ScriptActionType actionType, DbObject dbObject) => actionType switch
        {
            ScriptActionType.Create => Create.Invoke(dbObject),
            ScriptActionType.Alter => Alter.Invoke(dbObject), // drop dependencies, alter object, re-create dependencies
            ScriptActionType.Drop => Drop.Invoke(dbObject), // drop dependencies, drop object
            _ => throw new NotSupportedException()
        };
    }
}
