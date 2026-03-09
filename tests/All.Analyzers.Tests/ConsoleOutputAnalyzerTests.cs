using All.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace All.Analyzers.Tests;

/// <summary>
/// Tests for ALL001 — Console output detection.
/// Validates that Console.Write*, Console.Error.Write* are flagged.
/// </summary>
public class ConsoleOutputAnalyzerTests
{
    [Fact]
    public async Task ConsoleWriteLine_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ConsoleOutputAnalyzer, DefaultVerifier>
        {
            TestCode = @"
using System;

class Test
{
    void M()
    {
        {|#0:Console.WriteLine(""hello"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL001", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Console.WriteLine"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ConsoleWrite_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ConsoleOutputAnalyzer, DefaultVerifier>
        {
            TestCode = @"
using System;

class Test
{
    void M()
    {
        {|#0:Console.Write(42)|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL001", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Console.Write"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ConsoleErrorWriteLine_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ConsoleOutputAnalyzer, DefaultVerifier>
        {
            TestCode = @"
using System;

class Test
{
    void M()
    {
        {|#0:Console.Error.WriteLine(""error"")|};
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL001", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Console.Error.WriteLine"));
        await test.RunAsync();
    }

    [Fact]
    public async Task RegularMethodCall_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ConsoleOutputAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Test
{
    void DoWork() { }
    void M()
    {
        DoWork();
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task MemberAccessNotConsole_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<ConsoleOutputAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class MyWriter
{
    public void WriteLine(string s) { }
}

class Test
{
    void M()
    {
        var writer = new MyWriter();
        writer.WriteLine(""hello"");
    }
}",
        };
        await test.RunAsync();
    }
}
