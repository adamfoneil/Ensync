using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;
using System.Diagnostics;

namespace Ensync.Core;

public enum ScriptActionType
{
	Create,
	Alter,
	Drop
}

[DebuggerDisplay("{Action}: {Object}")]
public class ScriptAction
{
	public ScriptAction(ScriptActionType action, DbObject @object)
	{
		Action = action;
		Object = @object;
	}

	public ScriptActionType Action { get; init; }
	public DbObject Object { get; init; }
	/// <summary>
	/// the AffectedObject is usually the Object, but it will be different when the Statement
	/// is dropping or rebuilding a dependency
	/// </summary>
	public IEnumerable<(string Sql, DbObject? AffectedObject)> Statements { get; init; } = Enumerable.Empty<(string Sql, DbObject? AffectedObject)>();
	/// <summary>
	/// creates a warning when dropping a table with data
	/// </summary>
	public bool IsDestructive { get; init; }
	/// <summary>
	/// tells you number rows that will be lost/affected with the drop
	/// </summary>
	public string? Message { get; init; }

	public override bool Equals(object? obj)
	{
		if (obj is ScriptAction action)
		{
			return action.Action == Action && action.Object.Equals(Object);
		}

		return false;
	}

	public override int GetHashCode() => (Action.ToString() + Object.Name).GetHashCode();

	public ScriptActionKey ToScriptActionKey() => new(Action, Object.Name, Object.Type);
}

public record ScriptActionKey(ScriptActionType Action, string ObjectName, DbObjectType ObjectType)
{
	public ScriptAction ToScriptAction() => new(Action, new Placeholder(ObjectType, ObjectName));
}
