// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientAssemblyNamespaceAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0001Tests
    {
        private readonly string message = "Namespace '{0}' shouldn't contain public types."
            + " Use one of the following pre-approved namespace groups (https://azure.github.io/azure-sdk/registered_namespaces.html):"
            + " Azure.AI, Azure.Analytics, Azure.Communication, Azure.Compute, Azure.Containers, Azure.Core.Expressions, Azure.Data, Azure.Developer,"
            + " Azure.DigitalTwins, Azure.Health, Azure.Identity, Azure.IoT, Azure.Maps, Azure.Media, Azure.Messaging, Azure.MixedReality,"
            + " Azure.Monitor, Azure.ResourceManager, Azure.Search, Azure.Security, Azure.Storage, Azure.Verticals,"
            + " Microsoft.Extensions.Azure";

        [Fact]
        public async Task AZC0001ProducedForInvalidNamespaces()
        {
            const string code = @"
namespace RandomNamespace
{
    public class Program { }
}";

            var diagnostic = Verifier.Diagnostic("AZC0001")
                .WithMessage(string.Format(this.message, "RandomNamespace"))
                .WithSpan(2, 11, 2, 26);

            await Verifier.VerifyAnalyzerAsync(code, diagnostic);
        }

        [Fact]
        public async Task AZC0001ProducedForInvalidNamespaceWithValidRoot()
        {
            const string code = @"
namespace Azure.StorageBadNamespace
{
    public class Program { }
}";

            var diagnostic = Verifier.Diagnostic("AZC0001")
                .WithMessage(string.Format(this.message, "Azure.StorageBadNamespace"))
                .WithSpan(2, 17, 2, 36);

            await Verifier.VerifyAnalyzerAsync(code, diagnostic);
        }

        [Fact]
        public async Task AZC0001ProducedForInvalidSubNamespaceWithValidRoot()
        {
            const string code = @"
namespace Azure.StorageBadNamespace.Child
{
    public class Program { }
}";

            var diagnostic = Verifier.Diagnostic("AZC0001")
                .WithMessage(string.Format(this.message, "Azure.StorageBadNamespace.Child"))
                .WithSpan(2, 37, 2, 42);

            await Verifier.VerifyAnalyzerAsync(code, diagnostic);
        }

        [Fact]
        public async Task AZC0001ProducedOneErrorPerNamespaceDefinition()
        {
            const string code = @"
namespace {|AZC0001:RandomNamespace|}
{
    public class Program { }
}

namespace {|AZC0001:RandomNamespace|}
{
    public class Program2 { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForNamespacesWithPrivateMembersOnly()
        {
            const string code = @"
namespace RandomNamespace
{
    internal class Program { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForAllowedNamespaces()
        {
            const string code = @"
namespace Azure.Storage
{
    public class Program { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForAllowedSubNamespaces()
        {
            const string code = @"
namespace Azure.Storage.Hello
{
    public class Program { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForAzureCoreExpressions()
        {
            const string code = @"
namespace Azure.Core.Expressions
{
    public class Program { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForAzureCoreExpressionsSubNamespace()
        {
            const string code = @"
namespace Azure.Core.Expressions.Foobar
{
    public class Program { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForSubNamespacesOfAzureTemplate()
        {
            const string code = @"
namespace Azure.Template.RandomNamespace
{
    public class Program { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForAzureTemplateRoot()
        {
            const string code = @"
namespace Azure.Template
{
    public class Program { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForAzureTemplateSubNamespace()
        {
            const string code = @"
namespace Azure.Template.Models
{
    public class Program { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
