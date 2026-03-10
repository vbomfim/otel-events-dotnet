using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OtelEvents.Analyzers
{
    /// <summary>
    /// OTEL004: Detects string literals that look like event names but don't match
    /// any schema-defined event.
    /// TODO: Requires schema context integration to validate against .otel.yaml definitions.
    /// Currently registered but never fires — awaiting schema-aware infrastructure.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UndefinedEventNameAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OTEL004";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Undefined event name",
            messageFormat: "Event name '{0}' does not match any schema-defined event. Verify it is defined in .otel.yaml.",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "String literal that looks like an event name doesn't match any schema-defined event.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/OTEL004.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            // TODO: Register syntax/semantic action once schema context is available.
            // This analyzer requires loading .otel.yaml schema definitions at analysis time
            // to compare event name literals against known schema events.
        }
    }
}
