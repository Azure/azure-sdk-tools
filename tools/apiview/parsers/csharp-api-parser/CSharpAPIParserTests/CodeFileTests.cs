using System.Reflection;
using ApiView;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using APIView.Model.V2;
using Microsoft.CodeAnalysis;
using NuGet.ContentModel;


namespace CSharpAPIParserTests
{
    public class CodeFileTests
    {
        private readonly CodeFile templateCodeFile;
        private Assembly templateAssembly { get; set; }
        private readonly CodeFile storageCodeFile;
        public Assembly storageAssembly { get; set; }
        private readonly CodeFile coreCodeFile;
        public Assembly coreAssembly { get; set; }

        public CodeFileTests()
        {
            templateAssembly = Assembly.Load("Azure.Template");
            var dllStream = templateAssembly.GetFile("Azure.Template.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            this.templateCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            this.storageAssembly = Assembly.Load("Azure.Storage.Blobs");
            dllStream = storageAssembly.GetFile("Azure.Storage.Blobs.dll");
            assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            this.storageCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            this.coreAssembly = Assembly.Load("Azure.Core");
            dllStream = coreAssembly.GetFile("Azure.Core.dll");
            assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            this.coreCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);
        }

        [Fact]
        public void TestPackageName()
        {
            Assert.Equal("Azure.Template", templateCodeFile.PackageName);
        }

        [Fact]
        public void TestPackageVersion()
        {
            Assert.Equal("12.21.2.0", storageCodeFile.PackageVersion);
        }

        [Fact]
        public void TestLanguage()
        {
            Assert.Equal("C#", storageCodeFile.Language);
        }

        [Fact]
        public void TestTopLevelReviewLineCount()
        {
            Assert.Equal(8, templateCodeFile.ReviewLines.Count());
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
        public void TestAllClassesHaveEndOfContextLine()
        {
            // If current line is for class then next line at same level is expected to be a end of context line
            var lines = coreCodeFile.ReviewLines;
            var namespaceLine = lines.Where(lines => lines.LineId == "Azure").FirstOrDefault();
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

                expectEndOfContext = classLines[i].Tokens.Any(t=> (t.RenderClasses.Contains("class") ||
                    t.RenderClasses.Contains("struct") ||
                    t.RenderClasses.Contains("interface")) && !classLines[i].Tokens.Any( t=>t.Value == "abstract"));
            }
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

        [Fact]
        public void TestCodeFileJsonSchema()
        {
            //Verify JSON file generated for Azure.Template
            var isValid = validateSchema(templateCodeFile);
            Assert.True(isValid);
        }

        [Fact]
        public void TestCodeFileJsonSchema2()
        {
            //Verify JSON file generated for Azure.Storage.Blobs
            var storageAssembly = Assembly.Load("Azure.Storage.Blobs");
            var dllStream = storageAssembly.GetFile("Azure.Storage.Blobs.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            var storageCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);
            var isValid = validateSchema(storageCodeFile);
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
            Assert.Equal(8, CountNavigationNodes(parsedCodeFile.ReviewLines));
        }

        private int  CountNavigationNodes(List<ReviewLine> lines)
        {
            int count = 0;
            foreach (var line in lines)
            {
                var navTokens = line.Tokens.Where(x=> x.NavigationDisplayName != null);
                count += navTokens.Count(x => x.RenderClasses.Any());
                count += CountNavigationNodes(line.Children);
            }
            return count;
        }
    }
}
