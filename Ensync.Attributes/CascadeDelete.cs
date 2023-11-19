namespace Ensync.Attributes;

/// <summary>
/// denotes a foreign key that supports cascade deletes
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class CascadeDeleteAttribute : Attribute
{
}
