using OtelEvents.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace OtelEvents.Analyzers.Tests;

/// <summary>
/// Tests for OTEL003 — String interpolation in event field detection.
/// Validates that $"..." in log/emit methods is flagged as Error.
/// </summary>
public class StringInterpolationAnalyzerTests
{
    [Fact]
    public async Task InterpolatedStringInLogInformation_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<StringInterpolationAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Logger
{
    public void LogInformation(string message) { }
}

class Test
{
    void M()
    {
        var name = ""Alice"";
        var logger = new Logger();
        logger.LogInformation({|#0:$""Hello {name}""|});
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("LogInformation"));
        await test.RunAsync();
    }

    [Fact]
    public async Task InterpolatedStringInEmitMethod_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<StringInterpolationAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Events
{
    public void EmitOrderCreated(string description) { }
}

class Test
{
    void M()
    {
        var orderId = ""ord-123"";
        var events = new Events();
        events.EmitOrderCreated({|#0:$""Order {orderId} created""|});
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("EmitOrderCreated"));
        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateStringInLog_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<StringInterpolationAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Logger
{
    public void LogInformation(string message, params object[] args) { }
}

class Test
{
    void M()
    {
        var logger = new Logger();
        logger.LogInformation(""Hello {Name}"", ""Alice"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task InterpolatedStringInNonLogMethod_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<StringInterpolationAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Test
{
    void Process(string message) { }
    void M()
    {
        var name = ""Alice"";
        Process($""Hello {name}"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task InterpolatedStringInLogError_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<StringInterpolationAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Logger
{
    public void LogError(string message) { }
}

class Test
{
    void M()
    {
        var error = ""timeout"";
        var logger = new Logger();
        logger.LogError({|#0:$""Failed: {error}""|});
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("LogError"));
        await test.RunAsync();
    }
}
