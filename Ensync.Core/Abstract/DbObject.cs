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
    public int ParentId { get; init; }    
    public abstract DbObjectType Type { get; }
    public string Name { get; init; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is DbObject dbObj)
        {
            return (string.Compare(dbObj.Name, Name, true) == 0);
        }

        return false;
    }

    public override int GetHashCode() => Name.ToLower().GetHashCode();    
}
