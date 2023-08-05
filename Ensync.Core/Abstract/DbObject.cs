using Ensync.Core.Models;

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
    public int Id { get; init; }    
    public abstract DbObjectType Type { get; }
    public string Name { get; init; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is DbObject dbObj)
        {
            return Type == dbObj.Type && Name.Equals(dbObj.Name, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public override int GetHashCode() => $"{Type}.{Name.ToLower()}".GetHashCode();

    public virtual IEnumerable<DbObject> GetDependencies(Schema schema) => Enumerable.Empty<DbObject>();
}
