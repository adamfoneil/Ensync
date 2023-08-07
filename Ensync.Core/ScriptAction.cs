using Ensync.Core.Abstract;

namespace Ensync.Core;

public enum ScriptActionType
{
    Create,
    Alter,
    Drop
}

public class ScriptAction
{
    public ScriptAction(ScriptActionType action, DbObject @object)
    {
        Action = action;
        Object = @object;
    }

    public ScriptActionType Action { get; init; }
    public DbObject Object { get; init; }
    public required IEnumerable<string> Statements { get; init; }
}
