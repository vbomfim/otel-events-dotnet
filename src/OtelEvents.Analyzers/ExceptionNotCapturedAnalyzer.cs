using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OtelEvents.Analyzers
{
    /// <summary>
    /// ALL006: Detects catch blocks that don't capture the exception in an event emission.
    /// Flags catch clauses where the declared exception variable is never referenced,
    /// suggesting the exception is swallowed without proper logging or event emission.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExceptionNotCapturedAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ALL006";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Exception not captured",
            messageFormat: "Caught exception '{0}' is not passed to any event or logging method. Emit an otel-events event with the exception.",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "catch block doesn't emit an otel-events event with the caught exception.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/ALL006.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        }

        private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;

            // catch { } — no declaration at all, exception is silently swallowed
            if (catchClause.Declaration == null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation(), "exception"));
                return;
            }

            // catch (Exception) { } — type declared but no variable name
            if (catchClause.Declaration.Identifier.IsMissing)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation(),
                        catchClause.Declaration.Type?.ToString() ?? "exception"));
                return;
            }

            var exceptionVarName = catchClause.Declaration.Identifier.Text;
            var block = catchClause.Block;
            if (block == null)
                return;

            // Check if the exception variable is referenced anywhere in the catch body
            var isExceptionReferenced = block.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(id => id.Identifier.Text == exceptionVarName);

            if (!isExceptionReferenced)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation(), exceptionVarName));
            }
        }
    }
}
