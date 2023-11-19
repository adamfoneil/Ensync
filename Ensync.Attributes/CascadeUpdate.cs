namespace Ensync.Attributes;

/// <summary>
/// denotes a foreign key that supports cascade updates
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class CascadeUpdateAttribute : Attribute
{
}
