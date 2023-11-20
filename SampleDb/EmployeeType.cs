using Dapper.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace SampleDb;

public class EmployeeType : IEntity<int>
{
    public int Id { get; set; }
    [Key]
    [MaxLength(50)]
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}