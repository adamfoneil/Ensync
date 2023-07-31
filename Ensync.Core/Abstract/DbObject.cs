namespace Ensync.Core.Abstract;

public enum ObjectType
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
    public abstract ObjectType Type { get; }
    public DbObject? Parent { get; init; }
    public string Name { get; init; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is DbObject dbObj)
        {
            return ParentsAreEqual(dbObj, this) && (string.Compare(dbObj.Name, Name, true) == 0);
        }

        return false;
    }

    private static bool ParentsAreEqual(DbObject object1, DbObject object2)
    {
        if (object1.Parent == null ^ object2.Parent == null) return false;
        if (object1.Parent == null && object2.Parent == null) return true;

        try
        {
            return object1.Parent.Equals(object2.Parent);
        }
        catch
        {
            return false;
        }
    }
}
