// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ModelFactoryAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class ModelFactoryAnalyzerTests
    {
        [Theory]
        [InlineData("Response<TestModel>")]
        [InlineData("Task<Response<TestModel>>")]
        [InlineData("NullableResponse<TestModel>")]
        [InlineData("Operation<TestModel>")]
        [InlineData("Task<Operation<TestModel>>")]
        [InlineData("Pageable<TestModel>")]
        [InlineData("AsyncPageable<TestModel>")]
        public async Task AZC0035_ProducedWhenOutputModelMissingFromModelFactory(string returnType)
        {
            string code = $@"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{{
    public class {{|AZC0035:TestModel|}}
    {{
    }}

    public class TestClient
    {{
        public virtual {returnType} GetTestModel()
        {{
            return null;
        }}
    }}
}}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_NotProducedWhenOutputModelHasCorrespondingModelFactory()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class TestModel1
    {
    }

    public class TestModel2
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel1> GetTestModel1()
        {
            return null;
        }

        public virtual Task<Operation<TestModel2>> GetTestModel2Async()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        public static TestModel1 TestModel1()
        {
            return null;
        }

        public static TestModel2 TestModel2()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_IgnoresNonClientClassesAndBuiltInTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class TestModel
    {
    }

    public class SomeService
    {
        public virtual Response<TestModel> GetTestModel()
        {
            return null;
        }
    }

    public class TestClient
    {
        public virtual Response GetResponse()
        {
            return null;
        }

        public virtual Response<string> GetString()
        {
            return null;
        }

        public virtual Response<int> GetInt()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_ProducedForAzureTemplateSecretBundle()
        {
            const string code = @"
using Azure;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Template.Models;

namespace Azure.Template.Models
{
    public partial class {|AZC0035:SecretBundle|}
    {
        public string Value { get; }
        public string Id { get; }
        public string ContentType { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public string Kid { get; }
        public bool? Managed { get; }
    }
}

namespace Azure.Template
{
    public partial class TemplateClient
    {
        public virtual async Task<Response<SecretBundle>> GetSecretValueAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Response<SecretBundle> GetSecretValue(string secretName, CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_NotProducedForAzureTemplateWithModelFactory()
        {
            const string code = @"
using Azure;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Template.Models;

namespace Azure.Template.Models
{
    public partial class SecretBundle
    {
        public string Value { get; }
        public string Id { get; }
        public string ContentType { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public string Kid { get; }
        public bool? Managed { get; }
    }
}

namespace Azure.Template
{
    public partial class TemplateClient
    {
        public virtual async Task<Response<SecretBundle>> GetSecretValueAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Response<SecretBundle> GetSecretValue(string secretName, CancellationToken cancellationToken = default)
        {
            return null;
        }
    }

    public static class TemplateModelFactory
    {
        public static SecretBundle SecretBundle(string value = null, string id = null, string contentType = null, IReadOnlyDictionary<string, string> tags = null, string kid = null, bool? managed = null)
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }
    }
}