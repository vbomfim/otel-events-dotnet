using OtelEvents.Causality;

namespace OtelEvents.Causality.Tests;

/// <summary>
/// Tests for OtelEventsCausalityContext — AsyncLocal-based parent event ID management.
/// Validates scope nesting, async propagation, and thread isolation.
/// </summary>
public class OtelEventsCausalityContextTests
{
    [Fact]
    public void CurrentParentEventId_IsNullByDefault()
    {
        // Assert — no scope active
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void SetParent_SetsCurrentParentEventId()
    {
        // Arrange
        var parentId = "evt_test-parent-id";

        // Act
        using var scope = OtelEventsCausalityContext.SetParent(parentId);

        // Assert
        Assert.Equal(parentId, OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void SetParent_RestoresPreviousValueOnDispose()
    {
        // Arrange — no parent initially
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);

        // Act
        using (OtelEventsCausalityContext.SetParent("evt_parent-1"))
        {
            Assert.Equal("evt_parent-1", OtelEventsCausalityContext.CurrentParentEventId);
        }

        // Assert — restored to null
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void SetParent_SupportsNesting_RestoresEachLevel()
    {
        // Arrange
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);

        // Act & Assert — three levels of nesting
        using (OtelEventsCausalityContext.SetParent("evt_grandparent"))
        {
            Assert.Equal("evt_grandparent", OtelEventsCausalityContext.CurrentParentEventId);

            using (OtelEventsCausalityContext.SetParent("evt_parent"))
            {
                Assert.Equal("evt_parent", OtelEventsCausalityContext.CurrentParentEventId);

                using (OtelEventsCausalityContext.SetParent("evt_child"))
                {
                    Assert.Equal("evt_child", OtelEventsCausalityContext.CurrentParentEventId);
                }

                // Restored to parent
                Assert.Equal("evt_parent", OtelEventsCausalityContext.CurrentParentEventId);
            }

            // Restored to grandparent
            Assert.Equal("evt_grandparent", OtelEventsCausalityContext.CurrentParentEventId);
        }

        // Restored to null
        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public async Task SetParent_FlowsAcrossAsyncAwait()
    {
        // Arrange
        var parentId = "evt_async-parent";

        // Act
        using var scope = OtelEventsCausalityContext.SetParent(parentId);

        // Assert — flows across async boundaries
        await Task.Yield();
        Assert.Equal(parentId, OtelEventsCausalityContext.CurrentParentEventId);

        await Task.Delay(1);
        Assert.Equal(parentId, OtelEventsCausalityContext.CurrentParentEventId);

        var valueInTask = await Task.Run(() => OtelEventsCausalityContext.CurrentParentEventId);
        Assert.Equal(parentId, valueInTask);
    }

    [Fact]
    public async Task SetParent_FlowsAcrossAsyncAwait_WithNesting()
    {
        using (OtelEventsCausalityContext.SetParent("evt_outer"))
        {
            Assert.Equal("evt_outer", OtelEventsCausalityContext.CurrentParentEventId);

            await Task.Yield();
            Assert.Equal("evt_outer", OtelEventsCausalityContext.CurrentParentEventId);

            using (OtelEventsCausalityContext.SetParent("evt_inner"))
            {
                await Task.Yield();
                Assert.Equal("evt_inner", OtelEventsCausalityContext.CurrentParentEventId);
            }

            await Task.Yield();
            Assert.Equal("evt_outer", OtelEventsCausalityContext.CurrentParentEventId);
        }

        Assert.Null(OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public async Task SetParent_IsolatesBetweenConcurrentTasks()
    {
        // Arrange — two concurrent tasks with different parent IDs
        var barrier = new Barrier(2);

        var task1 = Task.Run(() =>
        {
            using var scope = OtelEventsCausalityContext.SetParent("evt_task1-parent");
            barrier.SignalAndWait(); // synchronize start
            Thread.Sleep(50); // overlap execution
            return OtelEventsCausalityContext.CurrentParentEventId;
        });

        var task2 = Task.Run(() =>
        {
            using var scope = OtelEventsCausalityContext.SetParent("evt_task2-parent");
            barrier.SignalAndWait(); // synchronize start
            Thread.Sleep(50); // overlap execution
            return OtelEventsCausalityContext.CurrentParentEventId;
        });

        // Act
        var results = await Task.WhenAll(task1, task2);

        // Assert — each task saw its own parent, not the other's
        Assert.Equal("evt_task1-parent", results[0]);
        Assert.Equal("evt_task2-parent", results[1]);
    }

    [Fact]
    public void SetParent_ThrowsOnNullParentEventId()
    {
        // Assert — null parent ID should throw
        Assert.Throws<ArgumentNullException>(() => OtelEventsCausalityContext.SetParent(null!));
    }

    [Fact]
    public void SetParent_ThrowsOnEmptyParentEventId()
    {
        // Assert — empty parent ID should throw
        Assert.Throws<ArgumentException>(() => OtelEventsCausalityContext.SetParent(""));
    }

    [Fact]
    public void CurrentParentEventId_CanBeSetDirectly()
    {
        // Arrange
        try
        {
            // Act
            OtelEventsCausalityContext.CurrentParentEventId = "evt_direct-set";
            Assert.Equal("evt_direct-set", OtelEventsCausalityContext.CurrentParentEventId);
        }
        finally
        {
            // Cleanup
            OtelEventsCausalityContext.CurrentParentEventId = null;
        }
    }
}
