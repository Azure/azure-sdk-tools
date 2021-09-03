// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.TaskCompletionSourceAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0013Tests
    {
        [Fact]
        public async Task AZC0013WarningOnTaskCompletionSourceDefaultConstructor()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private TaskCompletionSource<string> _tcs = {|AZC0013:new TaskCompletionSource<string>()|};
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0013WarningOnTaskCompletionSourceWithState()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>({|AZC0013:new object()|});
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0013WarningOnTaskCompletionSourceWithoutRunContinuationsAsynchronously()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>(new object(), {|AZC0013:TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness|});
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0013WarningOnTaskCompletionSourceWithField()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private static TaskCreationOptions _option = TaskCreationOptions.RunContinuationsAsynchronously;
        private TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>({|AZC0013:TaskCreationOptions.LongRunning | _option | TaskCreationOptions.PreferFairness|});
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0013NoWarningOnTaskCompletionSourceWithRunContinuationsAsynchronously()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0013NoWarningOnTaskCompletionSourceWithRunContinuationsAsynchronouslyAndState()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>(new object(), TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0013NoWarningOnTaskCompletionSourceContainsRunContinuationsAsynchronously()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    internal class RandomClass
    {
        private TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>(TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.PreferFairness);
    }
}
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}