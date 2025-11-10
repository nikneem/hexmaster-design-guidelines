using Hexmaster.DesignGuidelines.Core.Services;
using Xunit;

namespace Hexmaster.DesignGuidelines.Tests;

public class DocumentRegistryTests
{
    [Fact]
    public void Registry_ShouldContainRegisteredAdrs()
    {
        var all = DocumentRegistry.All;
        Assert.Contains(all, d => d.Id == "0001");
        Assert.Contains(all, d => d.Id == "0002");
    }
}
