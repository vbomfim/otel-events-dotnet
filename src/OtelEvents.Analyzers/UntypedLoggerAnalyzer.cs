using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OtelEvents.Analyzers
{
    /// <summary>
    /// ALL002: Detects direct ILogger.Log*, ILogger.LogInformation, etc.
    /// usage and suggests using otel-events schema-defined events instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UntypedLoggerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ALL002";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Untyped ILogger usage",
            messageFormat: "Direct '{0}' call detected. Use otel-events schema-defined events instead.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Direct ILogger.Log*, ILogger.LogInformation, etc. detected in application code. Use schema-defined events instead.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/ALL002.md");

        private static readonly ImmutableHashSet<string> LogMethodNames = ImmutableHashSet.Create(
            "LogTrace",
            "LogDebug",
            "LogInformation",
            "LogWarning",
            "LogError",
            "LogCritical");

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

            if (!LogMethodNames.Contains(methodName))
                return;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
        }
    }
}
