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
    public ScriptActionType Action { get; init; }
    public required DbObject Object { get; init; }
}
