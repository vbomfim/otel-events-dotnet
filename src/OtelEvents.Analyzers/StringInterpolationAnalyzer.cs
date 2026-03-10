using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OtelEvents.Analyzers
{
    /// <summary>
    /// OTEL003: Detects string interpolation ($"...") passed to log or otel-events generated
    /// event methods. otel-events handles message interpolation — pass raw values only.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StringInterpolationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OTEL003";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "String interpolation in event field",
            messageFormat: "String interpolation passed to '{0}'. Use structured logging with template parameters instead.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "String interpolation ($\"...\") passed to an otel-events-generated event method parameter. otel-events handles message interpolation — pass raw values only.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/OTEL003.md");

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

            var methodName = GetMethodName(invocation);
            if (methodName == null)
                return;

            if (!IsTargetMethod(methodName))
                return;

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is InterpolatedStringExpressionSyntax)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, argument.GetLocation(), methodName));
                }
            }
        }

        private static string GetMethodName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.Text;

            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.Text;

            return null;
        }

        private static bool IsTargetMethod(string methodName) =>
            LogMethodNames.Contains(methodName)
            || methodName.StartsWith("Emit");
    }
}
