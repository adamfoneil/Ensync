namespace Ensync.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class CalculatedAttribute(string expression) : Attribute
{
	public string Expression { get; } = expression;
}
