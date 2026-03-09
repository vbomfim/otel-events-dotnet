using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace All.Analyzers
{
    /// <summary>
    /// ALL009: Detects PII field names used in telemetry or logging contexts without
    /// a redaction policy. Schema fields with sensitivity: pii or sensitivity: credential
    /// should have EnvironmentProfile or RedactPatterns configured.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PiiWithoutRedactionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ALL009";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "PII field without redaction policy",
            messageFormat: "Field '{0}' appears to contain PII. Configure EnvironmentProfile or RedactPatterns for fields with sensitivity pii or credential.",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Schema field with sensitivity: pii or sensitivity: credential is used in code but no redaction policy is configured. Configure EnvironmentProfile or explicit RedactPatterns.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/ALL009.md");

        // Common PII-indicating patterns in field/tag names (lowercase)
        private static readonly string[] PiiPatterns = new[]
        {
            "email",
            "phone",
            "ssn",
            "social_security",
            "socialsecurity",
            "credit_card",
            "creditcard",
            "card_number",
            "cardnumber",
            "password",
            "passwd",
            "date_of_birth",
            "dateofbirth",
            "passport",
            "national_id",
            "nationalid",
            "ip_address",
            "ipaddress",
            "driver_license",
            "driverlicense"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
        }

        private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            var value = literal.Token.ValueText;

            if (!ContainsPiiPattern(value))
                return;

            // Only flag when used as a method argument (likely a tag/field name in telemetry)
            if (literal.Parent is ArgumentSyntax argument
                && argument.Parent is ArgumentListSyntax argumentList
                && argumentList.Parent is InvocationExpressionSyntax)
            {
                // Flag when the string is the first argument (typically the key/field name)
                if (argumentList.Arguments.Count > 0
                    && argumentList.Arguments[0] == argument)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, literal.GetLocation(), value));
                }
            }
        }

        private static bool ContainsPiiPattern(string value)
        {
            foreach (var pattern in PiiPatterns)
            {
                if (value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
