using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace All.Analyzers
{
    /// <summary>
    /// ALL005: Detects events defined in the schema that are never called in the codebase.
    /// TODO: Requires cross-file schema analysis and whole-project scanning.
    /// Currently registered but never fires — awaiting schema-aware infrastructure.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedEventAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ALL005";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Unused event definition",
            messageFormat: "Event '{0}' is defined in the schema. It is never emitted in code.",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Schema defines an event that is never called in the codebase.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/ALL005.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            // TODO: Register compilation-end action once schema context is available.
            // This analyzer requires:
            //   1. Loading .all.yaml to enumerate all defined events
            //   2. Scanning the entire compilation for Emit* method calls
            //   3. Reporting events that have definitions but no callers
        }
    }
}
