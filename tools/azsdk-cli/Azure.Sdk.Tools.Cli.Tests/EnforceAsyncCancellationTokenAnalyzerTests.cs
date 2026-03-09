using Azure.Sdk.Tools.Cli.Analyzer;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Azure.Sdk.Tools.Cli.Analyzer.EnforceAsyncCancellationTokenAnalyzer,
    Microsoft.CodeAnalysis.Testing.Verifiers.NUnitVerifier>;

namespace Azure.Sdk.Tools.Cli.Tests;

[TestFixture]
public class EnforceAsyncCancellationTokenAnalyzerTests
{
    // ── Should report AZSDK001 ──────────────────────────────────────

    [Test]
    public async Task PublicAsyncTask_WithoutCancellationToken_Reports()
    {
        var test = @"
using System.Threading.Tasks;

public class C
{
    public async Task {|#0:DoWork|}()
    {
        await Task.CompletedTask;
    }
}";
        var expected = VerifyCS.Diagnostic(EnforceAsyncCancellationTokenAnalyzer.Id)
            .WithLocation(0)
            .WithArguments("DoWork");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Test]
    public async Task PublicAsyncTaskOfInt_WithoutCancellationToken_Reports()
    {
        var test = @"
using System.Threading.Tasks;

public class C
{
    public async Task<int> {|#0:GetValue|}()
    {
        await Task.CompletedTask;
        return 42;
    }
}";
        var expected = VerifyCS.Diagnostic(EnforceAsyncCancellationTokenAnalyzer.Id)
            .WithLocation(0)
            .WithArguments("GetValue");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Test]
    public async Task InternalAsyncTask_WithoutCancellationToken_Reports()
    {
        var test = @"
using System.Threading.Tasks;

public class C
{
    internal async Task {|#0:InternalWork|}()
    {
        await Task.CompletedTask;
    }
}";
        var expected = VerifyCS.Diagnostic(EnforceAsyncCancellationTokenAnalyzer.Id)
            .WithLocation(0)
            .WithArguments("InternalWork");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Test]
    public async Task PublicTaskReturning_NotAsyncKeyword_WithoutCancellationToken_Reports()
    {
        var test = @"
using System.Threading.Tasks;

public class C
{
    public Task {|#0:DoWork|}()
    {
        return Task.CompletedTask;
    }
}";
        var expected = VerifyCS.Diagnostic(EnforceAsyncCancellationTokenAnalyzer.Id)
            .WithLocation(0)
            .WithArguments("DoWork");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Test]
    public async Task PublicAsyncValueTask_WithoutCancellationToken_Reports()
    {
        var test = @"
using System.Threading.Tasks;

public class C
{
    public async ValueTask {|#0:DoWork|}()
    {
        await Task.CompletedTask;
    }
}";
        var expected = VerifyCS.Diagnostic(EnforceAsyncCancellationTokenAnalyzer.Id)
            .WithLocation(0)
            .WithArguments("DoWork");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    // ── Should NOT report AZSDK001 ──────────────────────────────────

    [Test]
    public async Task PublicAsyncTask_WithCancellationToken_NoDiagnostic()
    {
        var test = @"
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task PrivateAsyncTask_WithoutCancellationToken_NoDiagnostic()
    {
        var test = @"
using System.Threading.Tasks;

public class C
{
    private async Task DoWork()
    {
        await Task.CompletedTask;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task OverrideAsyncTask_WithoutCancellationToken_NoDiagnostic()
    {
        var test = @"
using System.Threading.Tasks;

public class Base
{
    public virtual async Task DoWork()
    {
        await Task.CompletedTask;
    }
}

public class Derived : Base
{
    public override async Task DoWork()
    {
        await Task.CompletedTask;
    }
}";
        // The base virtual method should flag, but the override should not.
        // We only expect one diagnostic on the base declaration.
        var expected = VerifyCS.Diagnostic(EnforceAsyncCancellationTokenAnalyzer.Id)
            .WithLocation(6, 31)
            .WithArguments("DoWork");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Test]
    public async Task StaticAsyncMain_NoDiagnostic()
    {
        var test = @"
using System.Threading.Tasks;

public class Program
{
    static async Task Main(string[] args)
    {
        await Task.CompletedTask;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task TestAttribute_NoDiagnostic()
    {
        var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class TestAttribute : Attribute { }

public class C
{
    [Test]
    public async Task Verify_Something()
    {
        await Task.CompletedTask;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task FactAttribute_NoDiagnostic()
    {
        var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class FactAttribute : Attribute { }

public class C
{
    [Fact]
    public async Task Verify_Something()
    {
        await Task.CompletedTask;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task NonAsyncVoidMethod_NoDiagnostic()
    {
        var test = @"
public class C
{
    public void DoWork() { }
    public string GetName() => ""hello"";
    public int GetValue() => 1;
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task CancellationTokenInMiddlePosition_NoDiagnostic()
    {
        var test = @"
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public async Task DoWork(string name, CancellationToken ct, int retries)
    {
        await Task.CompletedTask;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
