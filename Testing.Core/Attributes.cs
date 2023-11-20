using Ensync.Attributes;
using Ensync.Dotnet.Extensions;

namespace Testing.Core;

[TestClass]
public class Attributes
{
    [TestMethod]
    public void CheckForCalculatedAttribute()
    {
        var price = typeof(Sample).GetProperty(nameof(Sample.Price)) ?? throw new Exception("property not found");

        Assert.IsTrue(price.HasAttribute<CalculatedAttribute>(out var _));
    }

    private class Sample
    {
        [Calculated("hello hello")]
        public decimal Price { get; set; }
    }
}
