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
	public DbObject? Parent { get; set; }

	public virtual (bool Result, string? Message) IsAltered(DbObject compareWith) => (false, null);

	public override bool Equals(object? obj)
	{
		if (obj is DbObject dbObj)
		{
			if (ObjectId != default && ObjectId == dbObj.ObjectId) return true;
			return Type == dbObj.Type && Name.Equals(dbObj.Name, StringComparison.OrdinalIgnoreCase);
		}

		return false;
	}

	public override int GetHashCode() => $"{Type}.{Name.ToLower()}".GetHashCode();

	public override string ToString() => $"{Type} {Name}";
	
	public virtual IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema) => Enumerable.Empty<(DbObject? Parent, DbObject Child)>();
}
