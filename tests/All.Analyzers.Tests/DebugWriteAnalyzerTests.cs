using All.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace All.Analyzers.Tests;

/// <summary>
/// Tests for ALL007 — Debug.Write and Trace.Write detection.
/// Validates that Debug.Write*, Trace.Write* are flagged.
/// </summary>
public class DebugWriteAnalyzerTests
{
    [Fact]
    public async Task DebugWriteLine_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<DebugWriteAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Debug
{
    public static void WriteLine(string message) { }
}

class Test
{
    void M()
    {
        {|#0:Debug.WriteLine(""debug info"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL007", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Debug.WriteLine"));
        await test.RunAsync();
    }

    [Fact]
    public async Task DebugWrite_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<DebugWriteAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Debug
{
    public static void Write(string message) { }
}

class Test
{
    void M()
    {
        {|#0:Debug.Write(""debug"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL007", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Debug.Write"));
        await test.RunAsync();
    }

    [Fact]
    public async Task TraceWriteLine_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<DebugWriteAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Trace
{
    public static void WriteLine(string message) { }
}

class Test
{
    void M()
    {
        {|#0:Trace.WriteLine(""trace info"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL007", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Trace.WriteLine"));
        await test.RunAsync();
    }

    [Fact]
    public async Task TraceTraceError_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<DebugWriteAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Trace
{
    public static void TraceError(string message) { }
}

class Test
{
    void M()
    {
        {|#0:Trace.TraceError(""error"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL007", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Trace.TraceError"));
        await test.RunAsync();
    }

    [Fact]
    public async Task RegularStaticMethod_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<DebugWriteAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class MyHelper
{
    public static void Write(string data) { }
}

class Test
{
    void M()
    {
        MyHelper.Write(""data"");
    }
}",
        };
        await test.RunAsync();
    }
}
