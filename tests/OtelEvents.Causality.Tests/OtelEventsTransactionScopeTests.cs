using OtelEvents.Causality;

namespace OtelEvents.Causality.Tests;

/// <summary>
/// Tests for OtelEventsTransactionScope — transaction lifecycle management
/// with AsyncLocal scope isolation and outcome tracking.
/// </summary>
public class OtelEventsTransactionScopeTests
{
    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE — Start → Success → Duration recorded
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Begin_CreatesActiveScope()
    {
        using var scope = OtelEventsTransactionScope.Begin("test.transaction");

        Assert.Equal("test.transaction", scope.TransactionName);
        Assert.NotNull(scope.EventId);
        Assert.StartsWith("evt_", scope.EventId);
        Assert.False(scope.IsCompleted);
        Assert.Null(scope.Outcome);
        Assert.Null(scope.OutcomeCategory);
    }

    [Fact]
    public void Begin_RegistersInActiveScopes()
    {
        using var scope = OtelEventsTransactionScope.Begin("test.transaction");

        Assert.True(OtelEventsTransactionScope.TryGetActive("test.transaction", out var active));
        Assert.Same(scope, active);
    }

    [Fact]
    public void TryComplete_MarksAsSuccess()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");

        var completed = OtelEventsTransactionScope.TryComplete("new.order", "order.shipped");

        Assert.True(completed);
        Assert.True(scope.IsCompleted);
        Assert.Equal("order.shipped", scope.Outcome);
        Assert.Equal("success", scope.OutcomeCategory);
    }

    [Fact]
    public void TryComplete_RecordsDuration()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");

        // Wait a small amount to ensure duration > 0
        Thread.Sleep(10);

        OtelEventsTransactionScope.TryComplete("new.order", "order.shipped");

        Assert.True(scope.ElapsedMilliseconds >= 5, $"Expected ≥5ms, got {scope.ElapsedMilliseconds}ms");
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE — Start → Failure → Duration recorded
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TryFail_MarksAsFailure()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");

        var failed = OtelEventsTransactionScope.TryFail("new.order", "order.payment.declined");

        Assert.True(failed);
        Assert.True(scope.IsCompleted);
        Assert.Equal("order.payment.declined", scope.Outcome);
        Assert.Equal("failure", scope.OutcomeCategory);
    }

    [Fact]
    public void TryFail_RecordsDuration()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");

        Thread.Sleep(10);

        OtelEventsTransactionScope.TryFail("new.order", "order.payment.declined");

        Assert.True(scope.ElapsedMilliseconds >= 5, $"Expected ≥5ms, got {scope.ElapsedMilliseconds}ms");
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE — Start → Abandoned → Duration recorded
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_WithoutComplete_MarksAsAbandoned()
    {
        var scope = OtelEventsTransactionScope.Begin("new.order");

        scope.Dispose();

        Assert.True(scope.IsCompleted);
        Assert.Equal("abandoned", scope.OutcomeCategory);
        Assert.Null(scope.Outcome);
    }

    [Fact]
    public void Dispose_RemovesFromActiveScopes()
    {
        var scope = OtelEventsTransactionScope.Begin("new.order");

        scope.Dispose();

        Assert.False(OtelEventsTransactionScope.TryGetActive("new.order", out _));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var scope = OtelEventsTransactionScope.Begin("new.order");

        scope.Dispose();
        scope.Dispose(); // Should not throw

        Assert.Equal("abandoned", scope.OutcomeCategory);
    }

    // ═══════════════════════════════════════════════════════════════
    // EDGE CASES — Already completed/failed, not found
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TryComplete_AlreadyCompleted_ReturnsFalse()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");
        OtelEventsTransactionScope.TryComplete("new.order", "order.shipped");

        var secondComplete = OtelEventsTransactionScope.TryComplete("new.order", "order.another");

        Assert.False(secondComplete);
        Assert.Equal("order.shipped", scope.Outcome); // First outcome preserved
    }

    [Fact]
    public void TryFail_AlreadyCompleted_ReturnsFalse()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");
        OtelEventsTransactionScope.TryComplete("new.order", "order.shipped");

        var failed = OtelEventsTransactionScope.TryFail("new.order", "order.failed");

        Assert.False(failed);
        Assert.Equal("success", scope.OutcomeCategory); // First outcome preserved
    }

    [Fact]
    public void TryComplete_NonExistentTransaction_ReturnsFalse()
    {
        var result = OtelEventsTransactionScope.TryComplete("non.existent", "outcome");

        Assert.False(result);
    }

    [Fact]
    public void TryFail_NonExistentTransaction_ReturnsFalse()
    {
        var result = OtelEventsTransactionScope.TryFail("non.existent", "outcome");

        Assert.False(result);
    }

    [Fact]
    public void TryGetActive_NonExistentTransaction_ReturnsFalse()
    {
        var result = OtelEventsTransactionScope.TryGetActive("non.existent", out var scope);

        Assert.False(result);
        Assert.Null(scope);
    }

    // ═══════════════════════════════════════════════════════════════
    // NESTED TRANSACTIONS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void NestedTransactions_IndependentLifecycles()
    {
        using var outer = OtelEventsTransactionScope.Begin("outer.transaction");
        using var inner = OtelEventsTransactionScope.Begin("inner.transaction");

        // Complete inner without affecting outer
        OtelEventsTransactionScope.TryComplete("inner.transaction", "inner.done");

        Assert.True(inner.IsCompleted);
        Assert.Equal("success", inner.OutcomeCategory);

        Assert.False(outer.IsCompleted);
        Assert.Null(outer.OutcomeCategory);
    }

    [Fact]
    public void NestedTransactions_BothAccessibleByName()
    {
        using var outer = OtelEventsTransactionScope.Begin("outer.transaction");
        using var inner = OtelEventsTransactionScope.Begin("inner.transaction");

        Assert.True(OtelEventsTransactionScope.TryGetActive("outer.transaction", out _));
        Assert.True(OtelEventsTransactionScope.TryGetActive("inner.transaction", out _));
    }

    // ═══════════════════════════════════════════════════════════════
    // CAUSAL SCOPE INTEGRATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Begin_SetsCausalParentEventId()
    {
        using var scope = OtelEventsTransactionScope.Begin("new.order");

        Assert.Equal(scope.EventId, OtelEventsCausalityContext.CurrentParentEventId);
    }

    [Fact]
    public void Dispose_RestoresPreviousCausalParent()
    {
        var previousParent = OtelEventsCausalityContext.CurrentParentEventId;

        using (OtelEventsTransactionScope.Begin("new.order"))
        {
            Assert.NotNull(OtelEventsCausalityContext.CurrentParentEventId);
        }

        Assert.Equal(previousParent, OtelEventsCausalityContext.CurrentParentEventId);
    }

    // ═══════════════════════════════════════════════════════════════
    // ASYNC LOCAL ISOLATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AsyncLocal_IsolatesScopesAcrossParallelRequests()
    {
        var task1 = Task.Run(() =>
        {
            using var scope = OtelEventsTransactionScope.Begin("request.one");
            Thread.Sleep(50);
            Assert.True(OtelEventsTransactionScope.TryGetActive("request.one", out _));
            Assert.False(OtelEventsTransactionScope.TryGetActive("request.two", out _));
            OtelEventsTransactionScope.TryComplete("request.one", "request.one.done");
            return scope.OutcomeCategory;
        });

        var task2 = Task.Run(() =>
        {
            using var scope = OtelEventsTransactionScope.Begin("request.two");
            Thread.Sleep(50);
            Assert.True(OtelEventsTransactionScope.TryGetActive("request.two", out _));
            Assert.False(OtelEventsTransactionScope.TryGetActive("request.one", out _));
            OtelEventsTransactionScope.TryFail("request.two", "request.two.failed");
            return scope.OutcomeCategory;
        });

        var results = await Task.WhenAll(task1, task2);

        Assert.Equal("success", results[0]);
        Assert.Equal("failure", results[1]);
    }

    [Fact]
    public async Task AsyncLocal_ScopeFlowsAcrossAwait()
    {
        using var scope = OtelEventsTransactionScope.Begin("async.transaction");

        await Task.Yield();

        Assert.True(OtelEventsTransactionScope.TryGetActive("async.transaction", out var active));
        Assert.Same(scope, active);

        OtelEventsTransactionScope.TryComplete("async.transaction", "async.done");
        Assert.True(scope.IsCompleted);
    }

    [Fact]
    public async Task AsyncLocal_NestedAsyncScopes()
    {
        using var outer = OtelEventsTransactionScope.Begin("outer.async");

        await Task.Yield();

        using var inner = OtelEventsTransactionScope.Begin("inner.async");

        await Task.Yield();

        Assert.True(OtelEventsTransactionScope.TryGetActive("outer.async", out _));
        Assert.True(OtelEventsTransactionScope.TryGetActive("inner.async", out _));

        OtelEventsTransactionScope.TryComplete("inner.async", "inner.done");
        Assert.True(inner.IsCompleted);
        Assert.False(outer.IsCompleted);
    }
}
