using OtelEvents.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace OtelEvents.Analyzers.Tests;

/// <summary>
/// Tests for OTEL008 — Reserved "otel_events." prefix detection.
/// Validates that string literals with "otel_events." prefix in field name contexts are flagged as Error.
/// </summary>
public class ReservedPrefixAnalyzerTests
{
    [Fact]
    public async Task AllPrefixInMethodArgument_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ReservedPrefixAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Activity
{
    public void SetTag(string key, string value) { }
}

class Test
{
    void M()
    {
        var activity = new Activity();
        activity.SetTag({|#0:""otel_events.version""|}, ""1.0"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL008", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("otel_events.version"));
        await test.RunAsync();
    }

    [Fact]
    public async Task AllPrefixUpperCase_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ReservedPrefixAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Tags
{
    public void Add(string key, object value) { }
}

class Test
{
    void M()
    {
        var tags = new Tags();
        tags.Add({|#0:""otel_events.event_id""|}, ""123"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("OTEL008", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("otel_events.event_id"));
        await test.RunAsync();
    }

    [Fact]
    public async Task NonAllPrefix_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ReservedPrefixAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Activity
{
    public void SetTag(string key, string value) { }
}

class Test
{
    void M()
    {
        var activity = new Activity();
        activity.SetTag(""http.method"", ""GET"");
        activity.SetTag(""service.name"", ""api"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task AllPrefixWithSpace_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ReservedPrefixAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Test
{
    void Log(string message) { }
    void M()
    {
        Log(""otel_events. students should attend"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task AllPrefixNotInFieldContext_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ReservedPrefixAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Test
{
    void M()
    {
        // Just a string assignment, not a field name context
        var comment = ""otel_events.version is a reserved prefix"";
        _ = comment;
    }
}",
        };
        await test.RunAsync();
    }
}
