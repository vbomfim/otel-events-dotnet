using All.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace All.Analyzers.Tests;

/// <summary>
/// Tests for ALL008 — Reserved "all." prefix detection.
/// Validates that string literals with "all." prefix in field name contexts are flagged as Error.
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
        activity.SetTag({|#0:""all.version""|}, ""1.0"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL008", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("all.version"));
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
        tags.Add({|#0:""ALL.event_id""|}, ""123"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL008", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ALL.event_id"));
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
        Log(""all. students should attend"");
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
        var comment = ""all.version is a reserved prefix"";
        _ = comment;
    }
}",
        };
        await test.RunAsync();
    }
}
