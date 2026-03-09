using All.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace All.Analyzers.Tests;

/// <summary>
/// Tests for ALL009 — PII field without redaction policy detection.
/// Validates that PII-indicating field names in telemetry contexts are flagged.
/// </summary>
public class PiiWithoutRedactionAnalyzerTests
{
    [Fact]
    public async Task EmailFieldInSetTag_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<PiiWithoutRedactionAnalyzer, DefaultVerifier>
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
        activity.SetTag({|#0:""user.email""|}, ""alice@example.com"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL009", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("user.email"));
        await test.RunAsync();
    }

    [Fact]
    public async Task SsnFieldInAddTag_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<PiiWithoutRedactionAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Span
{
    public void AddTag(string key, string value) { }
}

class Test
{
    void M()
    {
        var span = new Span();
        span.AddTag({|#0:""customer.ssn""|}, ""123-45-6789"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL009", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("customer.ssn"));
        await test.RunAsync();
    }

    [Fact]
    public async Task PasswordFieldInLog_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<PiiWithoutRedactionAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Logger
{
    public void Emit(string field, string value) { }
}

class Test
{
    void M()
    {
        var logger = new Logger();
        logger.Emit({|#0:""user.password""|}, ""secret123"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL009", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("user.password"));
        await test.RunAsync();
    }

    [Fact]
    public async Task NonPiiField_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<PiiWithoutRedactionAnalyzer, DefaultVerifier>
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
        activity.SetTag(""request.id"", ""req-123"");
        activity.SetTag(""http.method"", ""GET"");
        activity.SetTag(""user.role"", ""admin"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task PiiStringNotAsFirstArgument_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<PiiWithoutRedactionAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Logger
{
    public void Log(string level, string detail) { }
}

class Test
{
    void M()
    {
        var logger = new Logger();
        logger.Log(""info"", ""user.email is required"");
    }
}",
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task CreditCardField_ReportsDiagnostic()
    {
        var test = new CSharpAnalyzerTest<PiiWithoutRedactionAnalyzer, DefaultVerifier>
        {
            TestCode = @"
class Tags
{
    public void Set(string name, string value) { }
}

class Test
{
    void M()
    {
        var tags = new Tags();
        tags.Set({|#0:""payment.credit_card""|}, ""4111-xxxx"");
    }
}",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("ALL009", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("payment.credit_card"));
        await test.RunAsync();
    }
}
