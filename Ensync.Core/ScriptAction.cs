﻿using Ensync.Core.Abstract;

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
    public required IEnumerable<(string Sql, DbObject? AffectedObject)> Statements { get; init; }
    /// <summary>
    /// creates a warning when dropping a table with data
    /// </summary>
    public bool IsDestructive { get; init; }
    /// <summary>
    /// tells you number rows that will be lost/affected with the drop
    /// </summary>
    public string? Message { get; init; }
}
