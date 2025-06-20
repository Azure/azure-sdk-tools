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
        public async Task AZC0035_IgnoresClientTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class BlobContainerClient
    {
        public string Name { get; set; }
        public string Uri { get; set; }
    }

    public class TestClient
    {
        public virtual Response<BlobContainerClient> GetBlobContainer()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_IgnoresEasilyInstantiableTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class BlobServiceProperties
    {
        public string Version { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class BlobImmutabilityPolicy
    {
        public BlobImmutabilityPolicy(string policyType, int retentionDays)
        {
            PolicyType = policyType;
            RetentionDays = retentionDays;
        }

        public string PolicyType { get; }
        public int RetentionDays { get; }
    }

    public class TestClient
    {
        public virtual Response<BlobServiceProperties> GetServiceProperties()
        {
            return null;
        }

        public virtual Response<BlobImmutabilityPolicy> GetImmutabilityPolicy()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_FlagsTypesWithPrivateConstructorOrNonSettableProperties()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class {|AZC0035:PrivateConstructorModel|}
    {
        private PrivateConstructorModel() { }
        public string Name { get; set; }
    }

    public class {|AZC0035:ReadOnlyPropertyModel|}
    {
        public string Name { get; }
    }

    public class TestClient
    {
        public virtual Response<PrivateConstructorModel> GetPrivateConstructorModel()
        {
            return null;
        }

        public virtual Response<ReadOnlyPropertyModel> GetReadOnlyPropertyModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_RealWorldExamples()
        {
            const string code = @"
using Azure;
using System;
using System.Threading.Tasks;

namespace Azure.Storage.Blobs
{
    // Client type - should be ignored
    public class BlobContainerClient
    {
        public string Name { get; set; }
        public string AccountName { get; set; }
    }

    // Easily instantiable via setters - should be ignored
    public class BlobServiceProperties
    {
        public string Version { get; set; }
        public bool LoggingEnabled { get; set; }
        public int RetentionDays { get; set; }
    }

    // Easily instantiable via constructor - should be ignored
    public class BlobImmutabilityPolicy
    {
        public BlobImmutabilityPolicy(string policyType, int retentionDays)
        {
            PolicyType = policyType;
            RetentionDays = retentionDays;
        }

        public string PolicyType { get; }
        public int RetentionDays { get; }
    }

    // Easily instantiable via constructor + setters - should be ignored
    public class ReleasedObjectInfo
    {
        public ReleasedObjectInfo(string objectId)
        {
            ObjectId = objectId;
        }

        public string ObjectId { get; }
        public string Status { get; set; }
        public DateTime? ReleasedAt { get; set; }
    }

    // Should still be flagged - private constructor
    public class {|AZC0035:PrivateConstructorModel|}
    {
        private PrivateConstructorModel() { }
        public string Name { get; set; }
    }

    // Should still be flagged - read-only properties without constructor params
    public class {|AZC0035:ReadOnlyModel|}
    {
        public string Name { get; }
        public int Value { get; }
    }

    public class TestClient
    {
        public virtual Response<BlobContainerClient> GetBlobContainer() => null;
        public virtual Response<BlobServiceProperties> GetServiceProperties() => null;
        public virtual Response<BlobImmutabilityPolicy> GetImmutabilityPolicy() => null;
        public virtual Response<ReleasedObjectInfo> GetReleasedObjectInfo() => null;
        public virtual Response<PrivateConstructorModel> GetPrivateConstructorModel() => null;
        public virtual Response<ReadOnlyModel> GetReadOnlyModel() => null;
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }
    }
}