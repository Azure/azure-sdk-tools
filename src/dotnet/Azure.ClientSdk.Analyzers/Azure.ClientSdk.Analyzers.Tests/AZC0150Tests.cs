// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ModelReaderWriterAotAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0150Tests
    {
        // Common class definitions shared across all tests
        private const string CommonImports = @"
using System;
";

        private const string CommonNamespace = @"
namespace System.ClientModel.Primitives
{";

        private const string CommonClassDefinitions = @"
    public class ModelReaderWriterContext {}
    public class ModelReaderWriterOptions 
    {
        public static ModelReaderWriterOptions Json { get; } = new ModelReaderWriterOptions();
    }
    public class BinaryData {}";

        private const string ReaderWriterDefinition = @"
    public class ModelReaderWriter
    {
        public static T Read<T>(BinaryData data, ModelReaderWriterOptions options = default) => default;
        public static T Read<T>(BinaryData data, ModelReaderWriterOptions options, ModelReaderWriterContext context) => default;
        public static object Read(BinaryData data, Type type, ModelReaderWriterOptions options = default) => default;
        public static object Read(BinaryData data, Type type, ModelReaderWriterOptions options, ModelReaderWriterContext context) => default;
        
        public static BinaryData Write<T>(T model, ModelReaderWriterOptions options = default) => default;
        public static BinaryData Write<T>(T model, ModelReaderWriterOptions options, ModelReaderWriterContext context) => default;
        public static BinaryData Write(object model, ModelReaderWriterOptions options = default) => default;
        public static BinaryData Write(object model, ModelReaderWriterOptions options, ModelReaderWriterContext context) => default;
    }";

        private const string NamespaceClosing = @"
}";

        // Helper method to generate test code with common parts
        private string GenerateTestCode(string testClassContent, string readerWriterDefinition = null, string ns = null)
        {
            return CommonImports +
                   (ns ?? CommonNamespace) +
                   CommonClassDefinitions +
                   (readerWriterDefinition ?? ReaderWriterDefinition) +
                   testClassContent +
                   NamespaceClosing;
        }

        #region Read<T> Method Tests

        [Fact]
        public async Task ReadGeneric_NoOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            
            // Should trigger diagnostic - no context used
            var myModel = {|AZC0150:ModelReaderWriter.Read<string>(data)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task ReadGeneric_NoOptions_ProducesDiagnostic_Internal()
        {
            var testClassContent = @"
    internal class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            
            // Should trigger diagnostic - no context used
            var myModel = {|AZC0150:ModelReaderWriter.Read<string>(data)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }


        [Fact]
        public async Task ReadGeneric_WithOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            
            // Should trigger diagnostic - no context used
            var myModel = {|AZC0150:ModelReaderWriter.Read<string>(data, options)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task ReadGeneric_WithOptionsAndContext_NoDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            var context = new ModelReaderWriterContext();
            
            // Should not trigger diagnostic - context is used
            var myModel = ModelReaderWriter.Read<string>(data, options, context);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        #endregion

        #region Read(Type) Method Tests

        [Fact]
        public async Task ReadType_NoOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            
            // Should trigger diagnostic - no context used
            var myModel = {|AZC0150:ModelReaderWriter.Read(data, typeof(string))|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task ReadType_WithOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            
            // Should trigger diagnostic - no context used
            var myModel = {|AZC0150:ModelReaderWriter.Read(data, typeof(string), options)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task ReadType_WithOptionsAndContext_NoDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            var context = new ModelReaderWriterContext();
            
            // Should not trigger diagnostic - context is used
            var myModel = ModelReaderWriter.Read(data, typeof(string), options, context);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        #endregion

        #region Write<T> Method Tests

        [Fact]
        public async Task WriteGeneric_NoOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var model = ""test"";
            
            // Should trigger diagnostic - no context used
            {|AZC0150:ModelReaderWriter.Write<string>(model)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WriteGeneric_WithOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var model = ""test"";
            var options = ModelReaderWriterOptions.Json;
            
            // Should trigger diagnostic - no context used
            {|AZC0150:ModelReaderWriter.Write<string>(model, options)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WriteGeneric_WithOptionsAndContext_NoDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var model = ""test"";
            var options = ModelReaderWriterOptions.Json;
            var context = new ModelReaderWriterContext();
            
            // Should not trigger diagnostic - context is used
            ModelReaderWriter.Write<string>(model, options, context);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        #endregion

        #region Write(object) Method Tests

        [Fact]
        public async Task WriteObject_NoOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var model = new object();
            
            // Should trigger diagnostic - no context used
            {|AZC0150:ModelReaderWriter.Write(model)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WriteObject_WithOptions_ProducesDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var model = new object();
            var options = ModelReaderWriterOptions.Json;
            
            // Should trigger diagnostic - no context used
            {|AZC0150:ModelReaderWriter.Write(model, options)|};
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WriteObject_WithOptionsAndContext_NoDiagnostic()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var model = new object();
            var options = ModelReaderWriterOptions.Json;
            var context = new ModelReaderWriterContext();
            
            // Should not trigger diagnostic - context is used
            ModelReaderWriter.Write(model, options, context);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        #endregion

        #region Special Cases Tests

        [Fact]
        public async Task WithDerivedContext_ReadGeneric_NoDiagnostic()
        {
            var testClassContent = @"
    public class DerivedContext : ModelReaderWriterContext {}

    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            var derivedContext = new DerivedContext();
            
            // Should not trigger diagnostic because DerivedContext inherits from ModelReaderWriterContext
            var myModel = ModelReaderWriter.Read<string>(data, options, derivedContext);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WithDerivedContext_ReadType_NoDiagnostic()
        {
            var testClassContent = @"
    public class DerivedContext : ModelReaderWriterContext {}

    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            var derivedContext = new DerivedContext();
            
            // Should not trigger diagnostic because DerivedContext inherits from ModelReaderWriterContext
            var myModel = ModelReaderWriter.Read(data, typeof(string), options, derivedContext);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WithDerivedContext_WriteGeneric_NoDiagnostic()
        {
            var testClassContent = @"
    public class CustomContext : ModelReaderWriterContext {}

    public class TestClass
    {
        public void TestMethod()
        {
            var model = ""test"";
            var options = ModelReaderWriterOptions.Json;
            var customContext = new CustomContext();
            
            // Should not trigger diagnostic because CustomContext inherits from ModelReaderWriterContext
            ModelReaderWriter.Write<string>(model, options, customContext);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task WithDerivedContext_WriteObject_NoDiagnostic()
        {
            var testClassContent = @"
    public class CustomContext : ModelReaderWriterContext {}

    public class TestClass
    {
        public void TestMethod()
        {
            var model = new object();
            var options = ModelReaderWriterOptions.Json;
            var customContext = new CustomContext();
            
            // Should not trigger diagnostic because CustomContext inherits from ModelReaderWriterContext
            ModelReaderWriter.Write(model, options, customContext);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent));
        }

        [Fact]
        public async Task NoDiagnosticForNonModelReaderWriterClass()
        {
            var customReaderWriterDefinition = @"
    public class JsonReaderWriter
    {
        public static T Read<T>(BinaryData data, ModelReaderWriterOptions options = default) => default;
        public static BinaryData Write<T>(T model, ModelReaderWriterOptions options = default) => default;
    }";

            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var model = ""test"";
            var options = ModelReaderWriterOptions.Json;
            
            // These should not trigger diagnostics - different class name
            var myModel = JsonReaderWriter.Read<string>(data, options);
            JsonReaderWriter.Write<string>(model, options);
        }
    }";

            await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent, customReaderWriterDefinition));
        }

        [Fact]
        public async Task NoDiagnosticForModelReaderWriterInDifferentNamespace()
        {
            var testClassContent = @"
    public class TestClass
    {
        public void TestMethod()
        {
            var data = new BinaryData();
            var options = ModelReaderWriterOptions.Json;
            var context = new ModelReaderWriterContext();
            
            // Should not trigger diagnostic - context is used
            var myModel = ModelReaderWriter.Read<string>(data, options, context);
        }
    }";

            var ns = @"
namespace Something.Wrong
{";
        await Verifier.VerifyAnalyzerAsync(GenerateTestCode(testClassContent, ns: ns));
        }

        #endregion
    }
}
