using OtelEvents.Causality;

namespace OtelEvents.Causality.Tests;

/// <summary>
/// Tests for OtelEventsCausalScope — convenience class for causal scope management.
/// </summary>
public class OtelEventsCausalScopeTests
{
    [Fact]
    public void Begin_SetsParentEventId()
    {
        // Arrange
        var eventId = "evt_scope-test";

        // Act
        using var scope = OtelEventsCausalScope.Begin(eventId);

        // Assert
        Assert.Equal(eventId, OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Begin_RestoresOnDispose()
    {
        // Arrange
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);

        // Act
        using (OtelEventsCausalScope.Begin("evt_temp"))
        {
            Assert.Equal("evt_temp", OtelEventsCausalityContext.CurrentParentEventId);
        }

        // Assert
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Begin_SupportsNesting()
    {
        using (OtelEventsCausalScope.Begin("evt_outer"))
        {
            using (OtelEventsCausalScope.Begin("evt_inner"))
            {
                Assert.Equal("evt_inner", OtelEventsCausalityContext.CurrentParentEventId);
            }

            Assert.Equal("evt_outer", OtelEventsCausalityContext.CurrentParentEventId);
        }

        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Begin_ThrowsOnNullEventId()
    {
        Assert.Throws<ArgumentNullException>(() => OtelEventsCausalScope.Begin(null!));
    }

    [Fact]
    public void Begin_ThrowsOnEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => OtelEventsCausalScope.Begin(""));
    }

    [Fact]
    public async Task Begin_FlowsAcrossAsyncBoundaries()
    {
        using var scope = OtelEventsCausalScope.Begin("evt_async");

        await Task.Yield();
        Assert.Equal("evt_async", OtelEventsCausalityContext.CurrentParentEventId);

        var result = await Task.Run(() => OtelEventsCausalityContext.CurrentParentEventId);
        Assert.Equal("evt_async", result);
    }
}
