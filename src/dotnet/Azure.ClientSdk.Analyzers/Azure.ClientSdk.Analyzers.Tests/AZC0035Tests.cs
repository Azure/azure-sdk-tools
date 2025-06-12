// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ModelFactoryAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class ModelFactoryAnalyzerTests
    {
        private const string DiagnosticId = "AZC0035";

        [Fact]
        public async Task AZC0035_NotProducedWhenOutputModelHasCorrespondingModelFactory()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class TestModel
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel> GetTestModel()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        public static TestModel TestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_ProducedWhenOutputModelMissingFromModelFactory()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class {|AZC0035:TestModel|}
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel> GetTestModel()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        public static string SomeOtherMethod()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_ProducedWhenNoModelFactoryExists()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class {|AZC0035:TestModel|}
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel> GetTestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_WorksWithTaskWrappedReturnTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class {|AZC0035:TestModel|}
    {
    }

    public class TestClient
    {
        public virtual Task<Response<TestModel>> GetTestModelAsync()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_WorksWithOperationReturnTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class TestModel
    {
    }

    public class TestClient
    {
        public virtual Operation<TestModel> StartOperation()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        public static TestModel TestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_WorksWithPageableReturnTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class TestModel
    {
    }

    public class TestClient
    {
        public virtual Pageable<TestModel> GetTestModels()
        {
            return null;
        }

        public virtual AsyncPageable<TestModel> GetTestModelsAsync()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        public static TestModel TestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_WorksWithMultipleModelsAndFactories()
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

    public class {|AZC0035:TestModel3|}
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel1> GetTestModel1()
        {
            return null;
        }

        public virtual Response<TestModel2> GetTestModel2()
        {
            return null;
        }

        public virtual Response<TestModel3> GetTestModel3()
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
    }

    public static class AnotherModelFactory
    {
        public static TestModel2 CreateTestModel2()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_IgnoresNonClientClasses()
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
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_IgnoresNonStaticModelFactoryClasses()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class {|AZC0035:TestModel|}
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel> GetTestModel()
        {
            return null;
        }
    }

    public class TestModelFactory
    {
        public TestModel TestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_IgnoresPrivateModelFactoryMethods()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class {|AZC0035:TestModel|}
    {
    }

    public class TestClient
    {
        public virtual Response<TestModel> GetTestModel()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        private static TestModel TestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }

        [Fact]
        public async Task AZC0035_IgnoresNonModelReturnTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
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
        public async Task AZC0035_WorksWithNullableResponseReturnTypes()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace Azure.Test
{
    public class TestModel
    {
    }

    public class TestClient
    {
        public virtual NullableResponse<TestModel> GetTestModel()
        {
            return null;
        }
    }

    public static class TestModelFactory
    {
        public static TestModel TestModel()
        {
            return null;
        }
    }
}";

            await Verifier.CreateAnalyzer(code).RunAsync();
        }
    }
}