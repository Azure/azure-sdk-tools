// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using APIView;
using APIView.Model;
using APIViewLegacy;
using FluentAssertions;
using Xunit;

namespace APIViewUnitTests
{
    public class CodeFileHtmlRendererXssTests
    {
        /// <summary>
        /// Verifies that a malicious DefinitionId cannot break out of the id attribute
        /// to inject event handlers (the exact payload from the MSRC report).
        /// </summary>
        [Fact]
        public void RenderToken_DefinitionId_WithAttributeInjection_IsEncoded()
        {
            // Arrange — payload from issue #15599
            var maliciousId = "x\" onmouseover=\"document.title='XSS'\" data-x=\"";
            var tokens = new[]
            {
                new CodeFileToken("HoverMe", CodeFileTokenKind.TypeName) { DefinitionId = maliciousId },
                new CodeFileToken("\n", CodeFileTokenKind.Newline)
            };

            var codeFile = new CodeFile
            {
                Tokens = tokens,
                Language = "Json",
                Name = "xss-test"
            };

            // Act
            var result = CodeFileHtmlRenderer.Normal.Render(codeFile);
            var html = result.CodeLines.First().DisplayString;

            // Assert — the id attribute value must be encoded so quotes don't break out
            // The word "onmouseover" exists inside the attribute value as harmless text,
            // but the quotes are encoded so it cannot become a real attribute.
            html.Should().Contain("&quot;"); // embedded quotes are encoded
            html.Should().Contain("id=\""); // the attribute itself still exists
            // Verify the attacker cannot break out: no unencoded quote followed by event handler
            html.Should().NotContain("\" onmouseover="); // raw breakout attempt is neutralized
        }

        /// <summary>
        /// Verifies that a malicious NavigateToId cannot break out of the href attribute.
        /// </summary>
        [Fact]
        public void RenderToken_NavigateToId_WithAttributeInjection_IsEncoded()
        {
            // Arrange
            var maliciousNavId = "x\" onclick=\"alert(1)\" data-x=\"";
            var tokens = new[]
            {
                new CodeFileToken("ClickMe", CodeFileTokenKind.MemberName) { NavigateToId = maliciousNavId },
                new CodeFileToken("\n", CodeFileTokenKind.Newline)
            };

            var codeFile = new CodeFile
            {
                Tokens = tokens,
                Language = "Json",
                Name = "xss-test"
            };

            // Act
            var result = CodeFileHtmlRenderer.Normal.Render(codeFile);
            var html = result.CodeLines.First().DisplayString;

            // Assert — quotes are encoded so the attacker cannot break out of href
            html.Should().Contain("&quot;"); // embedded quotes are encoded
            html.Should().Contain("href=\"#"); // the href structure is preserved
            html.Should().NotContain("\" onclick="); // raw breakout attempt is neutralized
        }

        /// <summary>
        /// Verifies that a malicious ExternalLinkStart token value cannot break out of the href attribute.
        /// </summary>
        [Fact]
        public void RenderToken_ExternalLinkStart_WithAttributeInjection_IsEncoded()
        {
            // Arrange — ExternalLinkStart uses token.Value as the href
            var maliciousUrl = "https://evil.com\" onmouseover=\"alert(1)\" data-x=\"";
            var tokens = new[]
            {
                new CodeFileToken(maliciousUrl, CodeFileTokenKind.ExternalLinkStart),
                new CodeFileToken("link text", CodeFileTokenKind.Text),
                new CodeFileToken(null, CodeFileTokenKind.ExternalLinkEnd),
                new CodeFileToken("\n", CodeFileTokenKind.Newline)
            };

            var codeFile = new CodeFile
            {
                Tokens = tokens,
                Language = "Json",
                Name = "xss-test"
            };

            // Act
            var result = CodeFileHtmlRenderer.Normal.Render(codeFile);
            var html = result.CodeLines.First().DisplayString;

            // Assert — quotes are encoded so the attacker cannot break out of href
            html.Should().Contain("&quot;"); // embedded quotes are encoded
            html.Should().Contain("<a target=\"_blank\" rel=\"noopener noreferrer\" href=\""); // structure preserved
            html.Should().NotContain("\" onmouseover="); // raw breakout attempt is neutralized
        }

        /// <summary>
        /// Verifies that normal (safe) DefinitionId and NavigateToId values render correctly.
        /// </summary>
        [Fact]
        public void RenderToken_SafeIds_RenderCorrectly()
        {
            // Arrange
            var tokens = new[]
            {
                new CodeFileToken("MyClass", CodeFileTokenKind.TypeName)
                {
                    DefinitionId = "Azure.Core.MyClass",
                    NavigateToId = "Azure.Core.OtherClass"
                },
                new CodeFileToken("\n", CodeFileTokenKind.Newline)
            };

            var codeFile = new CodeFile
            {
                Tokens = tokens,
                Language = "Json",
                Name = "safe-test"
            };

            // Act
            var result = CodeFileHtmlRenderer.Normal.Render(codeFile);
            var html = result.CodeLines.First().DisplayString;

            // Assert — safe IDs should render unmodified
            html.Should().Contain("id=\"Azure.Core.MyClass\"");
            html.Should().Contain("href=\"#Azure.Core.OtherClass\"");
            html.Should().Contain(">MyClass</a>");
        }

        /// <summary>
        /// Verifies that token.Value is still HTML-encoded (existing behavior preserved).
        /// </summary>
        [Fact]
        public void RenderToken_Value_WithHtmlChars_IsEncoded()
        {
            var tokens = new[]
            {
                new CodeFileToken("List<string>", CodeFileTokenKind.TypeName) { DefinitionId = "List_string" },
                new CodeFileToken("\n", CodeFileTokenKind.Newline)
            };

            var codeFile = new CodeFile
            {
                Tokens = tokens,
                Language = "Json",
                Name = "encode-test"
            };

            // Act
            var result = CodeFileHtmlRenderer.Normal.Render(codeFile);
            var html = result.CodeLines.First().DisplayString;

            // Assert — angle brackets in Value are encoded
            html.Should().Contain("&lt;string&gt;");
            html.Should().NotContain("<string>");
        }

        /// <summary>
        /// Verifies that external links include rel="noopener noreferrer" to prevent reverse tabnabbing.
        /// </summary>
        [Fact]
        public void RenderToken_ExternalLink_HasNoopenerNoreferrer()
        {
            var tokens = new[]
            {
                new CodeFileToken("https://learn.microsoft.com/dotnet", CodeFileTokenKind.ExternalLinkStart),
                new CodeFileToken("Documentation", CodeFileTokenKind.Text),
                new CodeFileToken(null, CodeFileTokenKind.ExternalLinkEnd),
                new CodeFileToken("\n", CodeFileTokenKind.Newline)
            };

            var codeFile = new CodeFile
            {
                Tokens = tokens,
                Language = "Json",
                Name = "tabnab-test"
            };

            // Act
            var result = CodeFileHtmlRenderer.Normal.Render(codeFile);
            var html = result.CodeLines.First().DisplayString;

            // Assert
            html.Should().Contain("rel=\"noopener noreferrer\"");
            html.Should().Contain("target=\"_blank\"");
            html.Should().Contain("href=\"https://learn.microsoft.com/dotnet\"");
        }
    }
}
