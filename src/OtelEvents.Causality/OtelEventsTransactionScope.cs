using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OtelEvents.Causality;

/// <summary>
/// A disposable transaction scope that wraps a causal scope with transaction lifecycle tracking.
/// Created by generated code for "start" events, resolved by "success"/"failure" events.
/// </summary>
/// <remarks>
/// Registered in an <see cref="AsyncLocal{T}"/> dictionary so success/failure events
/// can locate the active transaction by parent event name. Thread-safe by design.
/// </remarks>
public sealed class OtelEventsTransactionScope : IDisposable
{
    private static readonly AsyncLocal<Dictionary<string, OtelEventsTransactionScope>> s_activeScopes = new();

    private readonly CausalScopeHandle _causalScope;
    private readonly long _startTimestamp;
    private bool _disposed;

    /// <summary>The transaction name (typically the start event's dot-namespaced name).</summary>
    public string TransactionName { get; }

    /// <summary>The causal event ID from the underlying scope.</summary>
    public string EventId => _causalScope.EventId;

    /// <summary>Elapsed milliseconds since this transaction scope was created.</summary>
    public double ElapsedMilliseconds => Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;

    /// <summary>Whether this transaction has been completed (success or failure).</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// The outcome event name (e.g., "order.shipped" or "order.payment.declined").
    /// Null if transaction is still active or was abandoned.
    /// </summary>
    public string? Outcome { get; private set; }

    /// <summary>
    /// The outcome category: "success", "failure", or "abandoned".
    /// </summary>
    public string? OutcomeCategory { get; private set; }

    /// <summary>
    /// Gets the active scopes dictionary for the current async flow.
    /// Lazily creates the dictionary on first access.
    /// </summary>
    private static Dictionary<string, OtelEventsTransactionScope> ActiveScopes =>
        s_activeScopes.Value ??= new Dictionary<string, OtelEventsTransactionScope>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new transaction scope, beginning a causal scope and registering in the active scopes.
    /// </summary>
    /// <param name="causalScope">The underlying causal scope handle.</param>
    /// <param name="transactionName">The transaction name (start event name).</param>
    internal OtelEventsTransactionScope(CausalScopeHandle causalScope, string transactionName)
    {
        _causalScope = causalScope;
        TransactionName = transactionName;
        _startTimestamp = Stopwatch.GetTimestamp();
        ActiveScopes[transactionName] = this;
    }

    /// <summary>
    /// Begins a new transaction scope with an auto-generated event ID.
    /// </summary>
    /// <param name="transactionName">The transaction name (start event name).</param>
    /// <returns>A disposable transaction scope handle.</returns>
    public static OtelEventsTransactionScope Begin(string transactionName)
    {
        var causalScope = OtelEventsCausalScope.Begin();
        return new OtelEventsTransactionScope(causalScope, transactionName);
    }

    /// <summary>
    /// Tries to complete the transaction identified by <paramref name="parentName"/> as success.
    /// Records the outcome event name. No-op if the transaction is not found or already completed.
    /// </summary>
    /// <param name="parentName">The start event name identifying the transaction.</param>
    /// <param name="outcomeEvent">The success event name (e.g., "order.shipped").</param>
    /// <returns>True if the transaction was found and completed; false otherwise.</returns>
    public static bool TryComplete(string parentName, string outcomeEvent)
    {
        if (ActiveScopes.TryGetValue(parentName, out var scope) && !scope.IsCompleted)
        {
            scope.IsCompleted = true;
            scope.Outcome = outcomeEvent;
            scope.OutcomeCategory = "success";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to complete the transaction identified by <paramref name="parentName"/> as failure.
    /// Records the outcome event name. No-op if the transaction is not found or already completed.
    /// </summary>
    /// <param name="parentName">The start event name identifying the transaction.</param>
    /// <param name="outcomeEvent">The failure event name (e.g., "order.payment.declined").</param>
    /// <returns>True if the transaction was found and marked as failed; false otherwise.</returns>
    public static bool TryFail(string parentName, string outcomeEvent)
    {
        if (ActiveScopes.TryGetValue(parentName, out var scope) && !scope.IsCompleted)
        {
            scope.IsCompleted = true;
            scope.Outcome = outcomeEvent;
            scope.OutcomeCategory = "failure";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to get the active transaction scope for the given transaction name.
    /// </summary>
    /// <param name="transactionName">The start event name identifying the transaction.</param>
    /// <param name="scope">The active scope, if found.</param>
    /// <returns>True if an active scope exists; false otherwise.</returns>
    public static bool TryGetActive(string transactionName, out OtelEventsTransactionScope? scope)
    {
        return ActiveScopes.TryGetValue(transactionName, out scope);
    }

    /// <summary>
    /// Disposes the transaction scope.
    /// If not already completed/failed, marks as abandoned.
    /// Removes from active scopes and disposes the underlying causal scope.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!IsCompleted)
        {
            IsCompleted = true;
            OutcomeCategory = "abandoned";
        }

        ActiveScopes.Remove(TransactionName);
        _causalScope.Dispose();
    }
}
