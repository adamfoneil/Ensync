using System.Text.Json.Serialization;

namespace Ensync.Core.Abstract;

public enum DbObjectType
{
	Schema,
	Table,
	Column,
	Index,
	ForeignKey,
	CheckConstraint
}

public abstract class DbObject
{
	public int ObjectId { get; init; }
	public abstract DbObjectType Type { get; }
	public string Name { get; init; } = default!;
	[JsonIgnore]
	public DbObject? Parent { get; set; }
	/// <summary>
	/// this is for deserialization purposes within embedded test cases
	/// </summary>
	public string? ParentName { get; set; }

	public string FullName => string.Join(".", NameParts);
	private IEnumerable<string> NameParts => new[] { Parent?.Name ?? string.Empty, Name }.Where(val => !string.IsNullOrEmpty(val));

	public virtual (bool Result, string? Message) IsAltered(DbObject compareWith) => (false, null);

	public override bool Equals(object? obj)
	{
		if (obj is DbObject dbObj)
		{
			if (ObjectId != default && ObjectId == dbObj.ObjectId) return true;
			return Type == dbObj.Type && FullName.Equals(dbObj.FullName, StringComparison.OrdinalIgnoreCase);
		}

		return false;
	}

	public override int GetHashCode() => $"{Type}:{FullName.ToLower()}".GetHashCode();

	public override string ToString() => $"{Type} {FullName}";

	public virtual IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema, List<ScriptAction> actions) => Enumerable.Empty<(DbObject? Parent, DbObject Child)>();
}
