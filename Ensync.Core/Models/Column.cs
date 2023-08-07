using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Column : DbObject
{
    public override DbObjectType Type => DbObjectType.Column;
    public string DataType { get; init; } = default!;    
    public bool IsNullable { get; init; }
    public string? DefaultValue { get; init; }
    /// <summary>
    /// true when adding required column to table with data
    /// </summary>
    public bool IsDefaultRequired { get; init; }
    public int InternalId { get; init; }
}
