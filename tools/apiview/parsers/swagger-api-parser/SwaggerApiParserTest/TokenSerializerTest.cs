using System.Collections.Generic;
using System.Threading.Tasks;
using SwaggerApiParser;
using SwaggerApiParser.SwaggerApiView;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest 
{
    public class TokenSerializerTest
    {
        private readonly ITestOutputHelper output;

        public TokenSerializerTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public Task TestTokenSerializerPrimitiveType()
        {
            const string text = "hello";
            SerializeContext context = new SerializeContext();
            var ret = TokenSerializer.TokenSerialize(text, context);

            Assert.Equal(CodeFileTokenKind.Literal, ret[0].Kind);
            Assert.Equal(CodeFileTokenKind.Newline, ret[1].Kind);
            Assert.Equal("hello", ret[0].Value);
            return Task.CompletedTask;
        }

        [Fact]
        public Task TestTokenSerializerGeneral()
        {
            var general = new SwaggerApiViewGeneral { swagger = "2.0", info = { description = "sample", title = "sample swagger" } };
            SerializeContext context = new SerializeContext();

            var ret = TokenSerializer.TokenSerialize(general, context);

            // Assert first line format. 
            Assert.Equal(CodeFileTokenKind.Literal, ret[0].Kind);
            Assert.Equal(CodeFileTokenKind.Punctuation, ret[1].Kind);
            Assert.Equal("swagger", ret[0].Value);
            Assert.Equal(": ", ret[1].Value);
            Assert.Equal("2.0", ret[2].Value);

            return Task.CompletedTask;
        }

        [Fact]
        public Task TestTokenSerializerListObject()
        {
            var general = new SwaggerApiViewGeneral { swagger = "2.0", info = { description = "sample", title = "sample swagger" }, consumes = new List<string> { "application/json", "text/json" } };
            this.output.WriteLine(general.ToString());

            SerializeContext context = new SerializeContext();

            var ret = TokenSerializer.TokenSerialize(general, context);

            return Task.CompletedTask;
        }
    }
}


