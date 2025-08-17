using AgentHost.Shared.Util;
using Xunit;

namespace AgentHost.Worker.Tests;

public class SchemaHashUtilTests
{
    [Fact]
    public void SameContentDifferentOrderingProducesSameHash()
    {
        var a = new { b = 1, a = 2 };
        var bObj = new { a = 2, b = 1 };
        var h1 = SchemaHashUtil.Compute(a);
        var h2 = SchemaHashUtil.Compute(bObj);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void DifferentContentProducesDifferentHash()
    {
        var h1 = SchemaHashUtil.Compute(new { a = 1 });
        var h2 = SchemaHashUtil.Compute(new { a = 2 });
        Assert.NotEqual(h1, h2);
    }
}
