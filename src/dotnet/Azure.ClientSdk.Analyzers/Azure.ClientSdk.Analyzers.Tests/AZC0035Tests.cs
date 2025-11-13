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
        private TestModel() {{ }}
        public string Name {{ get; }}
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
        public async Task AZC0035_IgnoresEmptyClassesWithPublicConstructors()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    // Empty class with implicit public constructor - should NOT be flagged
    public class EmptyModel
    {
    }

    // Empty class with explicit public constructor - should NOT be flagged
    public class EmptyModelExplicit
    {
        public EmptyModelExplicit() { }
    }

    public class TestClient
    {
        public virtual Response<EmptyModel> GetEmptyModel()
        {
            return null;
        }

        public virtual Response<EmptyModelExplicit> GetEmptyModelExplicit()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_FlagsEmptyClassesWithNoPublicConstructor()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    // Empty class with no public constructor - should be flagged
    public class {|AZC0035:EmptyModelPrivate|}
    {
        private EmptyModelPrivate() { }
    }

    public class TestClient
    {
        public virtual Response<EmptyModelPrivate> GetEmptyModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_FlagsTypesWithPartiallySettableProperties()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    // This has a public constructor but not all properties can be set - should be flagged
    public class {|AZC0035:ExampleModel|}
    {
        public int Age { get; }
        public string Name { get; }

        public ExampleModel(string name)
        {
            Name = name;
        }
    }

    public class TestClient
    {
        public virtual Response<ExampleModel> GetExampleModel()
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

        [Fact]
        public async Task AZC0035_IgnoresSystemTypes()
        {
            const string code = @"
using Azure;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace System
{
    // System.BinaryData - should be ignored
    public class BinaryData
    {
        public string Content { get; }
    }
}

namespace System.Threading
{
    // System.Threading.Thread - should be ignored
    public class Thread
    {
        public string Name { get; set; }
        public bool IsAlive { get; }
    }
}

namespace Azure.Test
{
    public class TestClient
    {
        // These should NOT produce diagnostics because BinaryData and Thread are System types
        public virtual Response<BinaryData> GetBinaryData()
        {
            return null;
        }

        public virtual Task<Response<Thread>> GetThreadAsync()
        {
            return null;
        }

        // This should also work with generic unwrapping
        public virtual Task<Operation<BinaryData>> GetOperationOfBinaryDataAsync()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_AllowsSystemClientModelTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace System.ClientModel
{
    // Types in System.ClientModel should still be checked - this one should be flagged
    public class {|AZC0035:CustomClientModel|}
    {
        private CustomClientModel() { }
        public string Name { get; }
    }

    // This one should not be flagged - has public constructor with settable properties
    public class AllowedClientModel
    {
        public AllowedClientModel(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}

namespace System.ClientModel.Primitives
{
    // Types in System.ClientModel subnamespaces should also be checked now with the fix
    public class {|AZC0035:PrimitiveModel|}
    {
        private PrimitiveModel() { }
        public string Value { get; }
    }
}

namespace Azure.Test
{
    public class TestClient
    {
        public virtual Response<System.ClientModel.CustomClientModel> GetCustomClientModel()
        {
            return null;
        }

        public virtual Response<System.ClientModel.AllowedClientModel> GetAllowedClientModel()
        {
            return null;
        }

        public virtual Response<System.ClientModel.Primitives.PrimitiveModel> GetPrimitiveModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_GenericUnwrappingWithSystemTypes()
        {
            const string code = @"
using Azure;
using System;
using System.Threading.Tasks;

namespace System
{
    public class BinaryData
    {
        public string Content { get; }
    }
}

namespace Azure.Test
{
    // This should be flagged because it's not a System type and has no public constructor
    public class {|AZC0035:CustomModel|}
    {
        private CustomModel() { }
        public string Name { get; }
    }

    public class TestClient
    {
        // All these should NOT be flagged because BinaryData is a System type
        // even when wrapped in generics - unwrapping should still work correctly
        public virtual Task<Response<BinaryData>> GetBinaryDataInTaskResponseAsync()
        {
            return null;
        }

        public virtual Task<Operation<BinaryData>> GetBinaryDataInTaskOperationAsync()
        {
            return null;
        }

        public virtual Task<Pageable<BinaryData>> GetBinaryDataInTaskPageableAsync()
        {
            return null;
        }

        // This SHOULD be flagged because CustomModel is not a System type
        public virtual Task<Response<CustomModel>> GetCustomModelAsync()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_SystemClientModelGenericTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace System.ClientModel
{
    // System.ClientModel type that should be flagged
    public class {|AZC0035:ClientResult|}
    {
        private ClientResult() { }
        public string Value { get; }
    }

    // System.ClientModel type that should NOT be flagged (easily constructible)
    public class EasyClientModel
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
}

namespace Azure.Test
{
    public class TestClient
    {
        // ClientResult should be flagged even though it's in System.ClientModel
        public virtual Response<System.ClientModel.ClientResult> GetClientResult()
        {
            return null;
        }

        // EasyClientModel should NOT be flagged
        public virtual Response<System.ClientModel.EasyClientModel> GetEasyClientModel()
        {
            return null;
        }

        // Test unwrapping with System.ClientModel types
        public virtual Task<Response<System.ClientModel.ClientResult>> GetClientResultAsync()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }


        [Fact]
        public async Task AZC0035_OnlyAnalyzesSourceDefinedClientTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    // Source-defined model that should be flagged
    public class {|AZC0035:MyCustomModel|}
    {
        private MyCustomModel() { }
        public string Value { get; }
    }

    // Source-defined client - should be analyzed
    public class MyClient
    {
        public virtual Response<MyCustomModel> GetModel()
        {
            return null;
        }
    }

    // Source-defined model factory - should be analyzed
    public static class MyModelFactory
    {
        // This factory method doesn't cover MyCustomModel, so MyCustomModel should be flagged
        public static SomeOtherModel SomeOtherModel()
        {
            return null;
        }
    }

    public class SomeOtherModel
    {
        public string Name { get; set; }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }
    }
}
