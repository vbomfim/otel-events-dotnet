using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace All.Analyzers
{
    /// <summary>
    /// ALL001: Detects Console.Write*, Console.WriteLine, Console.Error.Write*
    /// usage and suggests using ALL-generated events instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConsoleOutputAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ALL001";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Console output detected",
            messageFormat: "'{0}' detected. Use ALL-generated events instead of console output.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Console.Write, Console.WriteLine, Console.Error.Write detected. Use ALL-generated events instead.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/ALL001.md");

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

            // Console.Write or Console.WriteLine
            if (memberAccess.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.Text == "Console"
                && IsWriteMethod(methodName))
            {
                ReportDiagnostic(context, invocation, "Console." + methodName);
                return;
            }

            // Console.Error.Write or Console.Error.WriteLine
            if (memberAccess.Expression is MemberAccessExpressionSyntax innerMember
                && innerMember.Expression is IdentifierNameSyntax innerIdentifier
                && innerIdentifier.Identifier.Text == "Console"
                && innerMember.Name.Identifier.Text == "Error"
                && IsWriteMethod(methodName))
            {
                ReportDiagnostic(context, invocation, "Console.Error." + methodName);
            }
        }

        private static bool IsWriteMethod(string methodName) =>
            methodName == "Write" || methodName == "WriteLine";

        private static void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            string memberName)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, invocation.GetLocation(), memberName));
        }
    }
}
