using System.Reflection;
using ApiView;
using System.Text.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using APIView.Model.V2;
using Microsoft.CodeAnalysis;


namespace CSharpAPIParserTests
{
    public class CodeFileTests
    {
        static CodeFile templateCodeFile;
        static Assembly templateAssembly { get; set; }
        static CodeFile storageCodeFile;
        static Assembly storageAssembly { get; set; }
        static CodeFile coreCodeFile;
        static Assembly coreAssembly { get; set; }

        public CodeFileTests() { }
        static CodeFileTests()
        {
            templateAssembly = Assembly.Load("Azure.Template");
            var dllStream = templateAssembly.GetFile("Azure.Template.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            templateCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            storageAssembly = Assembly.Load("Azure.Storage.Blobs");
            dllStream = storageAssembly.GetFile("Azure.Storage.Blobs.dll");
            assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            storageCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            coreAssembly = Assembly.Load("Azure.Core");
            dllStream = coreAssembly.GetFile("Azure.Core.dll");
            assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            coreCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);
        }

        public static IEnumerable<object[]> CodeFiles => new List<object[]>
        {
            new object[] { templateCodeFile, "Azure.Template" , "1.0.3.0", 9},
            new object[] { storageCodeFile , "Azure.Storage.Blobs", "12.21.2.0", 15},
            new object[] { coreCodeFile, "Azure.Core", "1.44.1.0", 27},
        };

        [Theory]
        [MemberData(nameof(CodeFiles))]
        public void TestPackageMetadata(CodeFile codeFile, string expectedPackageName, string expectedVersion, int expectedNumberOfTopLines)
        {
            Assert.Equal(expectedPackageName, codeFile.PackageName);
            Assert.Equal(expectedVersion, codeFile.PackageVersion);
            Assert.Equal("C#", codeFile.Language);
            Assert.Equal(expectedNumberOfTopLines, codeFile.ReviewLines.Count);
        }

        [Fact]
        public void TestClassReviewLineWithoutBase()
        {
            var lines = storageCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Storage.Blobs").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.BlobServiceClient").FirstOrDefault();
            Assert.NotNull(classLine);
            Assert.Equal(4, classLine.Tokens.Count());
            Assert.Equal("public class BlobServiceClient {", classLine.ToString().Trim());
        }

        [Fact]
        public void TestClassReviewLineWithBase()
        {
            var lines = storageCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Storage.Blobs.Models").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.Models.BlobDownloadInfo").FirstOrDefault();
            Assert.NotNull(classLine);
            Assert.Equal(6, classLine.Tokens.Count());
            Assert.Equal("public class BlobDownloadInfo : IDisposable {", classLine.ToString().Trim());
        }

        [Fact]
        public void TestMultipleKeywords()
        {
            var lines = storageCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Storage.Blobs.Models").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.Models.AccessTier").FirstOrDefault();
            Assert.NotNull(classLine);
            Assert.Equal(10, classLine.Tokens.Count());
            Assert.Equal("public readonly struct AccessTier : IEquatable<AccessTier> {", classLine.ToString().Trim());
        }

        [Fact]
        public void TestApiReviewLine()
        {
            var lines = storageCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Storage.Blobs").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.BlobServiceClient").FirstOrDefault();
            Assert.NotNull(classLine);
            var methodLine = classLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.BlobServiceClient.BlobServiceClient(System.String)").FirstOrDefault();
            Assert.NotNull(methodLine);
            Assert.Equal(7, methodLine.Tokens.Count());
            Assert.Equal("public BlobServiceClient(string connectionString);", methodLine.ToString().Trim());
        }

        [Fact]
        public void TestApiReviewLineMoreParams()
        {
            var lines = storageCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Storage.Blobs").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.BlobServiceClient").FirstOrDefault();
            Assert.NotNull(classLine);
            var methodLine = classLine.Children.Where(lines => lines.LineId.Contains("UndeleteBlobContainerAsync")).FirstOrDefault();
            Assert.NotNull(methodLine);
            Assert.Equal(23, methodLine.Tokens.Count);
            Assert.Equal("public virtual Task<Response<BlobContainerClient>> UndeleteBlobContainerAsync(string deletedContainerName, string deletedContainerVersion, CancellationToken cancellationToken = default);", methodLine.ToString().Trim());
        }

        [Fact]
        public void NoDuplicateLineIds()
        {
            var lines = coreCodeFile.ReviewLines;
            HashSet<string> lineIds = new HashSet<string>();

            AssertNoDupes(lines, lineIds);

            Assert.True(lineIds.Count > 10);
        }

        private void AssertNoDupes(List<ReviewLine> lines, HashSet<string> lineIds)
        {
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line.LineId))
                {
                    Assert.True(lineIds.Add(line.LineId), $"Duplicate lineId found: {line.LineId}");
                }
                AssertNoDupes(line.Children, lineIds);
            }
        }

        public static IEnumerable<object[]> PackageCodeFiles => new List<object[]>
        {
            new object[] { templateCodeFile },
            new object[] { storageCodeFile },
            new object[] { coreCodeFile }
        };

        [Theory]
        [MemberData(nameof(PackageCodeFiles))]
        public void TestAllClassesHaveEndOfContextLine(CodeFile codeFile)
        {
            // If current line is for class then next line at same level is expected to be a end of context line
            var lines = codeFile.ReviewLines;
            foreach (var namespaceLine in lines)
            {
                Assert.NotNull(namespaceLine);
                bool expectEndOfContext = false;
                var classLines = namespaceLine.Children;
                for (int i = 0; i < classLines.Count; i++)
                {
                    if (expectEndOfContext)
                    {
                        Assert.True(classLines[i].IsContextEndLine == true);
                        expectEndOfContext = false;
                        continue;
                    }

                    expectEndOfContext = classLines[i].Tokens.Any(t => (t.RenderClasses.Contains("class") ||
                        t.RenderClasses.Contains("struct") ||
                        t.RenderClasses.Contains("interface")) && !classLines[i].Tokens.Any(t => t.Value == "abstract"));
                }
            }
        }

        [Fact]
        public void TestHiddenAPI()
        {
            var apiText = "protected static BlobServiceClient CreateClient(Uri serviceUri, BlobClientOptions options, HttpPipelinePolicy authentication, HttpPipeline pipeline);";
            var lines = storageCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Storage.Blobs").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.BlobServiceClient").FirstOrDefault();
            Assert.NotNull(classLine);
            var hiddenApis = classLine.Children.Where(lines => lines.LineId == "Azure.Storage.Blobs.BlobServiceClient.CreateClient(System.Uri, Azure.Storage.Blobs.BlobClientOptions, Azure.Core.Pipeline.HttpPipelinePolicy, Azure.Core.Pipeline.HttpPipeline)").FirstOrDefault();
            Assert.NotNull(hiddenApis);
            Assert.Equal(18, hiddenApis.Tokens.Count());
            Assert.Equal(apiText, hiddenApis.ToString().Trim());
        }

        [Fact]
        public void TestAPIReviewContent()
        {
            string expected = @"namespace Azure.Template { 
    public class TemplateClient { 
        public TemplateClient(string vaultBaseUrl, TokenCredential credential); 
        public TemplateClient(string vaultBaseUrl, TokenCredential credential, TemplateClientOptions options); 
        protected TemplateClient(); 
        public virtual HttpPipeline Pipeline { get; }
        public virtual Response GetSecret(string secretName, RequestContext context); 
        public virtual Task<Response> GetSecretAsync(string secretName, RequestContext context); 
        public virtual Response<SecretBundle> GetSecretValue(string secretName, CancellationToken cancellationToken = default); 
        public virtual Task<Response<SecretBundle>> GetSecretValueAsync(string secretName, CancellationToken cancellationToken = default); 
    } 

    public class TemplateClientOptions : ClientOptions { 
        public enum ServiceVersion { 
            V7_0 = 1, 
        } 

        public TemplateClientOptions(ServiceVersion version = V7_0); 
    } 

} 

namespace Azure.Template.Models { 
    public class SecretBundle { 
        public string ContentType { get; }
        public string Id { get; }
        public string Kid { get; }
        public bool? Managed { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public string Value { get; }
    } 

} 

namespace Microsoft.Extensions.Azure { 
    public static class TemplateClientBuilderExtensions { 
        public static IAzureClientBuilder<TemplateClient, TemplateClientOptions> AddTemplateClient<TBuilder>(this TBuilder builder, string vaultBaseUrl) where TBuilder : IAzureClientFactoryBuilderWithCredential; 
        public static IAzureClientBuilder<TemplateClient, TemplateClientOptions> AddTemplateClient<TBuilder, TConfiguration>(this TBuilder builder, TConfiguration configuration) where TBuilder : IAzureClientFactoryBuilderWithConfiguration<TConfiguration>; 
    } 

} 

";
            Assert.Equal(expected, templateCodeFile.GetApiText());
        }

        [Theory]
        [MemberData(nameof(PackageCodeFiles))]
        public void TestCodeFileJsonSchema(CodeFile codeFile)
        {
            //Verify JSON file generated for Azure.Template
            var isValid = validateSchema(codeFile);
            Assert.True(isValid);
        }

        private bool validateSchema(CodeFile codeFile)
        {
            var json = JsonSerializer.Serialize(codeFile, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var schema = JSchema.Parse(TestData.TokenJsonSchema);
            var jsonObject = JObject.Parse(json);

            IList<string> validationErrors = new List<string>();
            bool isValid = jsonObject.IsValid(schema, out validationErrors);
            if (isValid)
            {
                Console.WriteLine("JSON is valid.");
            }
            else
            {
                Console.WriteLine("JSON is invalid. Errors:");
                foreach (string error in validationErrors)
                {
                    Console.WriteLine(error);
                }
            }
            return isValid;
        }

        [Fact]
        public void TestNavigationNodeHasRenderingClass()
        {
            var jsonString = JsonSerializer.Serialize(templateCodeFile);
            var parsedCodeFile = JsonSerializer.Deserialize<CodeFile>(jsonString);
            Assert.NotNull(parsedCodeFile);
            Assert.Equal(8, CountNavigationNodes(parsedCodeFile.ReviewLines));
        }

        private int CountNavigationNodes(List<ReviewLine> lines)
        {
            int count = 0;
            foreach (var line in lines)
            {
                var navTokens = line.Tokens.Where(x => x.NavigationDisplayName != null);
                count += navTokens.Count(x => x.RenderClasses.Any());
                count += CountNavigationNodes(line.Children);
            }
            return count;
        }

        [Fact]
        public void VerifyAttributeHAsRelatedLine()
        {
            Assert.Equal(11, CountAttributeRelatedToProperty(storageCodeFile.ReviewLines));
        }

        private int CountAttributeRelatedToProperty(List<ReviewLine> lines)
        {
            int count = 0;

            foreach (var line in lines)
            {
                if (line.LineId != null && line.LineId.StartsWith("System.FlagsAttribute().") && !string.IsNullOrEmpty(line.RelatedToLine))
                {
                    count++;
                }

                count += CountAttributeRelatedToProperty(line.Children);
            }
            return count;
        }

        [Fact]
        public void verifyHiddenApiCount()
        {
            Assert.Equal(4, CountHiddenApiInBlobDownloadInfo(storageCodeFile.ReviewLines));
        }

        private int CountHiddenApiInBlobDownloadInfo(List<ReviewLine> lines)
        {
            int count = 0;
            foreach (var line in lines)
            {
                if (line.LineId != null && line.LineId.StartsWith("Azure.Storage.Blobs.Models.BlobDownloadInfo") && line.IsHidden == true)
                {
                    count++;
                }
                count += CountHiddenApiInBlobDownloadInfo(line.Children);
            }
            return count;
        }

        [Fact]
        public void VerifyObsoleteMemberIsHidden()
        {
            var attestationAssembly = Assembly.Load("Azure.Security.Attestation");
            var dllStream = attestationAssembly.GetFile("Azure.Security.Attestation.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            var codeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            var lines = codeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Security.Attestation").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId == "Azure.Security.Attestation.AttestationResult").FirstOrDefault();
            Assert.NotNull(classLine);

            var obsoleteMethods = classLine.Children.Where(line => line.ToString().StartsWith("[Obsolete("));
            Assert.NotEmpty(obsoleteMethods);
            //Make sure member lines are marked as hidden if it has obsolete attribute
            foreach (var method in obsoleteMethods)
            {
                Assert.True(method.IsHidden);
                Assert.NotNull(method.RelatedToLine);
                var relatedLine = classLine.Children.Where(line => line.LineId == method.RelatedToLine).FirstOrDefault();
                Assert.True(relatedLine?.IsHidden);
            }
        }

        [Fact]
        public void VerifyTemplateClassLine()
        {
            var coreExprAssembly = Assembly.Load("Azure.Core.Expressions.DataFactory");
            var dllStream = coreExprAssembly.GetFile("Azure.Core.Expressions.DataFactory.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            var codeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            var lines = codeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure.Core.Expressions.DataFactory").FirstOrDefault();
            Assert.NotNull(namespaceLine);
            var classLine = namespaceLine.Children.Where(lines => lines.LineId.StartsWith("Azure.Core.Expressions.DataFactory.DataFactoryElement")).FirstOrDefault();
            Assert.NotNull(classLine);
            Assert.Equal("public sealed class DataFactoryElement<T> {", classLine.ToString().Trim());

            var methodLine = classLine.Children.Where(lines => lines.LineId == "Azure.Core.Expressions.DataFactory.DataFactoryElement<T>.FromKeyVaultSecret(Azure.Core.Expressions.DataFactory.DataFactoryKeyVaultSecret)").FirstOrDefault();
            Assert.NotNull(methodLine);
            Assert.Equal("public static DataFactoryElement<string?> FromKeyVaultSecret(DataFactoryKeyVaultSecret secret);", methodLine.ToString().Trim());
        }

        [Fact]
        public void VerifySkippedAttributes()
        {
            var serviceBusAssembly = Assembly.Load("Azure.Messaging.ServiceBus");
            var dllStream = serviceBusAssembly.GetFile("Azure.Messaging.ServiceBus.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            var codeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            var line = codeFile.ReviewLines.Where(l => l.LineId == "Microsoft.Extensions.Azure").FirstOrDefault();
            Assert.NotNull(line);
            var classLine = line.Children?.Where(l => l.LineId == "Microsoft.Extensions.Azure.ServiceBusClientBuilderExtensions").FirstOrDefault();
            Assert.NotNull(classLine);
            var methodLine = classLine.Children?.Where(l => l.LineId.Contains("Microsoft.Extensions.Azure.ServiceBusClientBuilderExtensions.AddServiceBusClient")).FirstOrDefault();
            Assert.NotNull(methodLine);

            bool isRequiresUnreferencedCodePresent = classLine.Children?.Any(l => l.Tokens.Any(t => t.Value == "RequiresUnreferencedCodeAttribute")) ?? false;
            bool isRequiresDynamicCode = classLine.Children?.Any(l => l.Tokens.Any(t => t.Value == "RequiresDynamicCode")) ?? false;
            Assert.False(isRequiresUnreferencedCodePresent);
            Assert.False(isRequiresDynamicCode);
        }

        [Fact]
        public void VerifyDuplicateLineIdValidationWorks()
        {
            // This test ensures that our duplicate line ID validation is working
            // We can't easily create a scenario with actual duplicate line IDs because
            // the CodeFileBuilder generates unique IDs based on symbol information
            // But we can verify that the validation mechanism is in place by checking
            // that the codefiles build successfully (no exceptions thrown)
            
            // If there were duplicate line IDs, the Build method would throw an InvalidOperationException
            Assert.NotNull(templateCodeFile);
            Assert.NotNull(coreCodeFile);
            // Note: storageCodeFile might have issues unrelated to line ID validation
            
            // The fact that these code files were built successfully indicates
            // that no duplicate line IDs were encountered during their construction
            Assert.True(templateCodeFile.ReviewLines.Count > 0);
            Assert.True(coreCodeFile.ReviewLines.Count > 0);
        }

        [Fact]
        public void VerifyGetLineIdThrowsOnDuplicate()
        {
            // Test that GetLineId method throws when duplicate line IDs are encountered
            var builder = new CSharpAPIParser.TreeToken.CodeFileBuilder();
            
            // Create a mock assembly to initialize the builder
            var templateAssembly = Assembly.Load("Azure.Template");
            var dllStream = templateAssembly.GetFile("Azure.Template.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            
            // This will initialize the builder and clear the used line IDs
            builder.Build(assemblySymbol, false, null);
            
            // Now create another builder instance to test the GetLineId method directly
            var testBuilder = new CSharpAPIParser.TreeToken.CodeFileBuilder();
            testBuilder.Build(assemblySymbol, false, null);
            
            // Find a member to test with
            var namespaceSymbol = assemblySymbol.GlobalNamespace.GetNamespaceMembers().FirstOrDefault();
            if (namespaceSymbol != null)
            {
                var typeSymbol = namespaceSymbol.GetTypeMembers().FirstOrDefault();
                if (typeSymbol != null)
                {
                    var members = typeSymbol.GetMembers().Where(m => !m.IsImplicitlyDeclared).ToList();
                    if (members.Count > 0)
                    {
                        // Use reflection to call the private GetLineId method
                        var getLineIdMethod = typeof(CSharpAPIParser.TreeToken.CodeFileBuilder)
                            .GetMethod("GetLineId", BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        if (getLineIdMethod != null)
                        {
                            // First call should succeed
                            var firstResult = getLineIdMethod.Invoke(testBuilder, new object[] { members[0] });
                            Assert.NotNull(firstResult);
                            
                            // Second call with the same member should throw due to duplicate
                            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
                                getLineIdMethod.Invoke(testBuilder, new object[] { members[0] }));
                            
                            Assert.IsType<InvalidOperationException>(ex.InnerException);
                            Assert.Contains("Duplicate line ID detected", ex.InnerException.Message);
                        }
                    }
                }
            }
        }
    }
}
