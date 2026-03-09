using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="AllSamplingProcessor"/>
/// in the OTEL logging pipeline.
/// </summary>
public static class AllSamplingExtensions
{
    /// <summary>
    /// Adds an <see cref="AllSamplingProcessor"/> to the logging pipeline,
    /// wrapping the specified inner processor. The sampler conditionally forwards
    /// log records to <paramref name="innerProcessor"/> based on probabilistic sampling.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="AllSamplingOptions"/>.
    /// If null, defaults are used (rate 1.0, head sampling — all events pass through).
    /// </param>
    /// <param name="innerProcessor">
    /// The downstream processor to forward sampled records to (e.g., a
    /// <see cref="SimpleExportProcessor{T}"/> or <see cref="BatchExportProcessor{T}"/>).
    /// </param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="innerProcessor"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// // Head sampling at 10% rate:
    /// var exporter = new AllJsonExporter(exporterOptions);
    /// var exportProcessor = new SimpleExportProcessor&lt;LogRecord&gt;(exporter);
    /// builder.AddAllSampler(
    ///     configure: opts =>
    ///     {
    ///         opts.Strategy = AllSamplingStrategy.Head;
    ///         opts.DefaultSamplingRate = 0.1;
    ///     },
    ///     innerProcessor: exportProcessor);
    ///
    /// // Tail sampling — always sample errors, 10% of successes:
    /// builder.AddAllSampler(
    ///     configure: opts =>
    ///     {
    ///         opts.Strategy = AllSamplingStrategy.Tail;
    ///         opts.DefaultSamplingRate = 0.1;
    ///         opts.AlwaysSampleErrors = true;
    ///     },
    ///     innerProcessor: exportProcessor);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddAllSampler(
        this LoggerProviderBuilder builder,
        Action<AllSamplingOptions>? configure,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        var options = new AllSamplingOptions();
        configure?.Invoke(options);

        return builder.AddProcessor(
            new AllSamplingProcessor(options, innerProcessor));
    }
}
