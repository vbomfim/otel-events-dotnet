using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace All.Analyzers
{
    /// <summary>
    /// ALL007: Detects Debug.Write*, Debug.WriteLine, Trace.Write*, Trace.WriteLine,
    /// Trace.TraceInformation, etc. and suggests using ALL-generated events instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DebugWriteAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ALL007";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Debug.Write detected",
            messageFormat: "'{0}' detected. Use ALL-generated events instead of debug/trace output.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Debug.Write*, Trace.Write* detected. Use ALL-generated events instead.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/ALL007.md");

        private static readonly ImmutableHashSet<string> DebugMethods = ImmutableHashSet.Create(
            "Write",
            "WriteLine",
            "Print");

        private static readonly ImmutableHashSet<string> TraceMethods = ImmutableHashSet.Create(
            "Write",
            "WriteLine",
            "TraceInformation",
            "TraceWarning",
            "TraceError");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return;

            var methodName = memberAccess.Name.Identifier.Text;

            if (memberAccess.Expression is IdentifierNameSyntax identifier)
            {
                var typeName = identifier.Identifier.Text;

                if (typeName == "Debug" && DebugMethods.Contains(methodName))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, invocation.GetLocation(), "Debug." + methodName));
                    return;
                }

                if (typeName == "Trace" && TraceMethods.Contains(methodName))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, invocation.GetLocation(), "Trace." + methodName));
                }
            }
        }
    }
}
