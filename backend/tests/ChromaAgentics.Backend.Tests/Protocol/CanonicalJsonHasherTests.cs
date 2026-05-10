using System.Text.Json;
using ChromaAgentics.Backend.Protocol;

namespace ChromaAgentics.Backend.Tests.Protocol;

public sealed class CanonicalJsonHasherTests
{
    [Fact]
    public void ComputeSha256_IgnoresObjectPropertyOrder()
    {
        using var first = JsonDocument.Parse("""{"title":"A","mode":"orchestrator"}""");
        using var second = JsonDocument.Parse("""{"mode":"orchestrator","title":"A"}""");

        Assert.Equal(
            CanonicalJsonHasher.ComputeSha256(first.RootElement),
            CanonicalJsonHasher.ComputeSha256(second.RootElement));
    }

    [Fact]
    public void ComputeSha256_ChangesWhenPayloadValuesChange()
    {
        using var first = JsonDocument.Parse("""{"title":"A","mode":"orchestrator"}""");
        using var second = JsonDocument.Parse("""{"title":"B","mode":"orchestrator"}""");

        Assert.NotEqual(
            CanonicalJsonHasher.ComputeSha256(first.RootElement),
            CanonicalJsonHasher.ComputeSha256(second.RootElement));
    }

    [Fact]
    public void ComputeSha256_SortsNestedObjectsAndPreservesArrayOrder()
    {
        using var first = JsonDocument.Parse(
            """{"items":[{"z":2,"a":1},{"name":"second"}],"meta":{"b":true,"a":null}}""");
        using var reorderedObjects = JsonDocument.Parse(
            """{"meta":{"a":null,"b":true},"items":[{"a":1,"z":2},{"name":"second"}]}""");
        using var reorderedArray = JsonDocument.Parse(
            """{"meta":{"a":null,"b":true},"items":[{"name":"second"},{"a":1,"z":2}]}""");

        var firstHash = CanonicalJsonHasher.ComputeSha256(first.RootElement);

        Assert.Equal(firstHash, CanonicalJsonHasher.ComputeSha256(reorderedObjects.RootElement));
        Assert.NotEqual(firstHash, CanonicalJsonHasher.ComputeSha256(reorderedArray.RootElement));
    }
}
