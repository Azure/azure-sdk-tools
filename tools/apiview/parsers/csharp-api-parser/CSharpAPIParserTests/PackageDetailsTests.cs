using System.Reflection;
using ApiView;

namespace CSharpAPIParserTests
{
    public class PackageDetailsTests
    {
        private CodeFile codeFile;
        public Assembly assembly { get; set; }

        public PackageDetailsTests()
        {
            assembly = Assembly.Load("Azure.Template");
            var dllStream = assembly.GetFile("Azure.Template.dll");
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, null);
            codeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, true, null);
        }

        [Fact]
        public void TestPackageName()
        {
            Assert.Equal("Azure.Template", codeFile.PackageName);
        }

        [Fact]
        public void TestTopLevelReviewLineCount()
        {
            Assert.Equal(8, codeFile.ReviewLines.Count());
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
            Assert.Equal(expected, codeFile.GetApiText());
        }
    }
}
