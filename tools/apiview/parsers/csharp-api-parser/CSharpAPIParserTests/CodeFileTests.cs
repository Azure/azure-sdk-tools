using System.Reflection;
using ApiView;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using APIView.Model.V2;


namespace CSharpAPIParserTests
{
    public class CodeFileTests
    {
        private readonly CodeFile templateCodeFile;
        private Assembly templateAssembly { get; set; }

        private readonly CodeFile storageCodeFile;
        public Assembly storageAssembly { get; set; }

        public CodeFileTests()
        {
            templateAssembly = Assembly.Load("Azure.Template");
            var dllStream = templateAssembly.GetFile("Azure.Template.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            this.templateCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);

            //Verify JSON file generated for Azure.Storage.Blobs
            this.storageAssembly = Assembly.Load("Azure.Storage.Blobs");
            dllStream = storageAssembly.GetFile("Azure.Storage.Blobs.dll");
            assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            this.storageCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);
        }

        [Fact]
        public void TestPackageName()
        {
            Assert.Equal("Azure.Template", templateCodeFile.PackageName);
        }

        [Fact]
        public void TestTopLevelReviewLineCount()
        {
            Assert.Equal(9, templateCodeFile.ReviewLines.Count());
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
        public void TestNavigatonNodeHasRenderingClass()
        {
            var jsonString = JsonSerializer.Serialize(templateCodeFile);
            var parsedCodeFile = JsonSerializer.Deserialize<CodeFile>(jsonString);
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
                if(line.LineId != null && line.LineId.StartsWith("System.FlagsAttribute.") && !string.IsNullOrEmpty(line.RelatedToLine))
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
    }
}
