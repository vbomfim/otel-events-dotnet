using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OtelEvents.Analyzers
{
    /// <summary>
    /// OTEL008: Detects string literals using the reserved "otel_events." prefix in field names.
    /// This prefix is reserved for otel-events library metadata and must not be used in application code.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ReservedPrefixAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OTEL008";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Reserved prefix usage",
            messageFormat: "Field name '{0}' uses the reserved 'otel_events.' prefix. This prefix is reserved for otel-events library metadata.",
            category: "Naming",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Code uses 'otel_events.' prefix in field names — this prefix is reserved for library metadata.",
            helpLinkUri: "https://github.com/otel-events-dotnet/blob/main/docs/analyzers/OTEL008.md");

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

            if (!IsReservedFieldName(value))
                return;

            // Only flag when used as a method argument or indexer key (likely a field name)
            if (IsFieldNameContext(literal))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, literal.GetLocation(), value));
            }
        }

        private static bool IsReservedFieldName(string value)
        {
            if (value.Length <= 4)
                return false;

            if (!value.StartsWith("otel_events.", StringComparison.OrdinalIgnoreCase))
                return false;

            // Ensure what follows "otel_events." is an identifier character (not whitespace or punctuation)
            char afterPrefix = value[4];
            return char.IsLetterOrDigit(afterPrefix) || afterPrefix == '_';
        }

        private static bool IsFieldNameContext(LiteralExpressionSyntax literal)
        {
            // Used as a method argument: SetTag("otel_events.version", ...)
            // Also covers indexer keys: dict["otel_events.version"]
            if (literal.Parent is ArgumentSyntax)
                return true;

            // Used in an initializer expression: { "otel_events.version", value }
            if (literal.Parent is InitializerExpressionSyntax)
                return true;

            return false;
        }
    }
}
