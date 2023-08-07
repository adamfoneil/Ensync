namespace Ensync.SqlServer.Internal;

internal class ForeignKeysResult
{
    public int ObjectId { get; set; }
    public int ReferencingObjectId { get; set; }
    public string ConstraintName { get; set; } = default!;
    public string ReferencedSchema { get; set; } = default!;
    public string ReferencedTable { get; set; } = default!;
    public string ReferencingSchema { get; set; } = default!;
    public string ReferencingTable { get; set; } = default!;
    public bool CascadeDelete { get; set; }
    public bool CascadeUpdate { get; set; }
}

internal class ForeignKeyColumnsResult
{
    public int ObjectId { get; set; }
    public string ReferencingName { get; set; } = default!;
    public string ReferencedName { get; set; } = default!;
}