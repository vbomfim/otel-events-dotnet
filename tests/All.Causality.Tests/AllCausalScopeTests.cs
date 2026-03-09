using All.Causality;

namespace All.Causality.Tests;

/// <summary>
/// Tests for AllCausalScope — convenience class for causal scope management.
/// </summary>
public class AllCausalScopeTests
{
    [Fact]
    public void Begin_SetsParentEventId()
    {
        // Arrange
        var eventId = "evt_scope-test";

        // Act
        using var scope = AllCausalScope.Begin(eventId);

        // Assert
        Assert.Equal(eventId, AllCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Begin_RestoresOnDispose()
    {
        // Arrange
        Assert.Null(AllCausalityContext.CurrentParentEventId);

        // Act
        using (AllCausalScope.Begin("evt_temp"))
        {
            Assert.Equal("evt_temp", AllCausalityContext.CurrentParentEventId);
        }

        // Assert
        Assert.Null(AllCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Begin_SupportsNesting()
    {
        using (AllCausalScope.Begin("evt_outer"))
        {
            using (AllCausalScope.Begin("evt_inner"))
            {
                Assert.Equal("evt_inner", AllCausalityContext.CurrentParentEventId);
            }

            Assert.Equal("evt_outer", AllCausalityContext.CurrentParentEventId);
        }

        Assert.Null(AllCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Begin_ThrowsOnNullEventId()
    {
        Assert.Throws<ArgumentNullException>(() => AllCausalScope.Begin(null!));
    }

    [Fact]
    public void Begin_ThrowsOnEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => AllCausalScope.Begin(""));
    }

    [Fact]
    public async Task Begin_FlowsAcrossAsyncBoundaries()
    {
        using var scope = AllCausalScope.Begin("evt_async");

        await Task.Yield();
        Assert.Equal("evt_async", AllCausalityContext.CurrentParentEventId);

        var result = await Task.Run(() => AllCausalityContext.CurrentParentEventId);
        Assert.Equal("evt_async", result);
    }
}
