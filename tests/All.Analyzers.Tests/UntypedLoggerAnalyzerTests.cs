using All.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace All.Analyzers.Tests;

/// <summary>
/// Tests for ALL002 — Untyped ILogger usage detection.
/// Validates that direct ILogger.LogXxx calls are flagged.
/// </summary>
public class UntypedLoggerAnalyzerTests
{
    [Fact]
    public async Task LogInformation_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UntypedLoggerAnalyzer, DefaultVerifier>
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
        var logger = new Logger();
        {|#0:logger.LogInformation(""Order created"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL002", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("LogInformation"));
        await test.RunAsync();
    }

    [Fact]
    public async Task LogError_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UntypedLoggerAnalyzer, DefaultVerifier>
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
        var logger = new Logger();
        {|#0:logger.LogError(""Something failed"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL002", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("LogError"));
        await test.RunAsync();
    }

    [Fact]
    public async Task LogWarning_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UntypedLoggerAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Logger
{
    public void LogWarning(string message) { }
}

class Test
{
    void M()
    {
        var logger = new Logger();
        {|#0:logger.LogWarning(""Slow response"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL002", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("LogWarning"));
        await test.RunAsync();
    }

    [Fact]
    public async Task RegularMethod_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UntypedLoggerAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Service
{
    public void Process(string data) { }
}

class Test
{
    void M()
    {
        var svc = new Service();
        svc.Process(""data"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task EmitMethod_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UntypedLoggerAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Events
{
    public void EmitOrderCreated(string orderId) { }
}

class Test
{
    void M()
    {
        var events = new Events();
        events.EmitOrderCreated(""ord-123"");
    }
}",
        };
        await test.RunAsync();
    }
}
