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
			return action.Action == Action && action.Object == Object;
		}

		return false;
	}

	public override int GetHashCode() => (Action.ToString() + Object.Name).GetHashCode();
}
