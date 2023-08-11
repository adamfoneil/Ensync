using Dapper.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleDb;

public class Employee : IEntity<int>
{
	public int Id { get; set; }
	[MaxLength(50)]
	public required string FirstName { get; set; }
	[MaxLength(50)]
	public required string LastName { get; set; }
	[ForeignKey(nameof(EmployeeType))]
	public int TypeId { get; set; }
	public DateTime HireDate { get; set; }
	public DateTime? TerminationDate { get; set; }
}
