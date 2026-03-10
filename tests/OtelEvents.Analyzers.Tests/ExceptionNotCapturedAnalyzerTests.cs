using OtelEvents.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace OtelEvents.Analyzers.Tests;

/// <summary>
/// Tests for OTEL006 — Exception not captured detection.
/// Validates that catch blocks without exception usage are flagged.
/// </summary>
public class ExceptionNotCapturedAnalyzerTests
{
    [Fact]
    public async Task CatchWithUnusedException_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ExceptionNotCapturedAnalyzer, DefaultVerifier>
        {
            TestCode = @"
using System;

class Test
{
    void M()
    {
        try { }
        {|#0:catch|} (Exception ex)
        {
            // Exception swallowed — not referenced
            var x = 1;
        }
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL006", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("ex"));
        await test.RunAsync();
    }

    [Fact]
    public async Task BareCatch_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ExceptionNotCapturedAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Test
{
    void M()
    {
        try { }
        {|#0:catch|}
        {
            var x = 1;
        }
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL006", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("exception"));
        await test.RunAsync();
    }

    [Fact]
    public async Task CatchWithExceptionUsed_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ExceptionNotCapturedAnalyzer, DefaultVerifier>
        {
            TestCode = @"
using System;

class Logger
{
    public void LogError(Exception ex) { }
}

class Test
{
    void M()
    {
        try { }
        catch (Exception ex)
        {
            var logger = new Logger();
            logger.LogError(ex);
        }
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task CatchWithExceptionMessageAccess_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ExceptionNotCapturedAnalyzer, DefaultVerifier>
        {
            TestCode = @"
using System;

class Test
{
    void Handle(string msg) { }
    void M()
    {
        try { }
        catch (Exception ex)
        {
            Handle(ex.Message);
        }
    }
}",
        };
        await test.RunAsync();
    }
}
