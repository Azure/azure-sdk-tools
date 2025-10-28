using System;
using System.Linq;
using System.Text.Json;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Models;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class AgentResponseParserTests
    {

        [Test]
        public void ParseErrors_WithInvalidInput_ThrowsInvalidOperationException()
        {
            // Test multiple invalid input scenarios
            Assert.Throws<ArgumentNullException>(() => AgentResponseParser.ParseErrors(null!).ToList());
            Assert.Throws<InvalidOperationException>(() => AgentResponseParser.ParseErrors(string.Empty).ToList());
            Assert.Throws<InvalidOperationException>(() => AgentResponseParser.ParseErrors("   \n\t  ").ToList());
            Assert.Throws<InvalidOperationException>(() => AgentResponseParser.ParseErrors("```json\n\n```").ToList());
        }


        [Test]
        public void ParseErrors_WithPlainTextAndJson_ParsesCorrectly()
        {
            // Arrange
            string response = @"here is the answer {""errors"": [{""type"": ""TestError"", ""message"": ""Test message""}]}";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("TestError"));
            Assert.That(result[0].message, Is.EqualTo("Test message"));
        }

        [Test]
        public void ParseErrors_WithTextAndMarkdownJsonBlock_ParsesCorrectly()
        {
            // Arrange
            string response = @"here is the answer ```json
{
    ""errors"": [
        {""type"": ""MarkdownError"", ""message"": ""Markdown test""}
    ]
}
```";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("MarkdownError"));
            Assert.That(result[0].message, Is.EqualTo("Markdown test"));
        }

        [Test]
        public void ParseErrors_WithEmptyMarkdownBlock_ThrowsInvalidOperationException()
        {
            // Arrange
            string response = "```json```";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AgentResponseParser.ParseErrors(response).ToList());
            Assert.That(ex?.Message, Does.Contain("Agent response is not in the expected JSON format"));
        }

        [Test]
        public void ParseErrors_WithEmptyMarkdownBlockWithNewlines_ThrowsInvalidOperationException()
        {
            // Arrange
            string response = "```json\n```";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AgentResponseParser.ParseErrors(response).ToList());
            Assert.That(ex?.Message, Does.Contain("Agent response is not in the expected JSON format"));
        }

        [Test]
        public void ParseErrors_WithPlainJsonContent_ParsesCorrectly()
        {
            // Arrange
            string response = @"{""errors"": [{""type"": ""PlainError"", ""message"": ""Plain JSON test""}]}";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("PlainError"));
            Assert.That(result[0].message, Is.EqualTo("Plain JSON test"));
        }

        [Test]
        public void ParseErrors_WithEmptyJsonObject_ReturnsEmptyCollection()
        {
            // Arrange
            string response = "{}";

            // Act
            var result = AgentResponseParser.ParseErrors(response);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseErrors_WithEmptyErrorsArray_ReturnsEmptyCollection()
        {
            // Arrange
            string response = @"{""errors"": []}";

            // Act
            var result = AgentResponseParser.ParseErrors(response);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseErrors_WithNullErrorsProperty_ReturnsEmptyCollection()
        {
            // Arrange
            string response = @"{""errors"": null}";

            // Act
            var result = AgentResponseParser.ParseErrors(response);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseErrors_WithValidAgentErrorResponse_ReturnsCorrectRuleErrors()
        {
            // Arrange
            string response = @"{
                ""errors"": [
                    {""type"": ""ValidationError"", ""message"": ""Missing required property""},
                    {""type"": ""SyntaxError"", ""message"": ""Invalid syntax on line 42""}
                ]
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].type, Is.EqualTo("ValidationError"));
            Assert.That(result[0].message, Is.EqualTo("Missing required property"));
            Assert.That(result[1].type, Is.EqualTo("SyntaxError"));
            Assert.That(result[1].message, Is.EqualTo("Invalid syntax on line 42"));
        }

        [Test]
        public void ParseErrors_WithValidJsonButWrongStructure_ReturnsEmptyCollection()
        {
            // Arrange
            string response = @"{""someOtherProperty"": ""value"", ""data"": [1, 2, 3]}";

            // Act
            var result = AgentResponseParser.ParseErrors(response);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseErrors_WithInvalidJsonSyntax_ThrowsInvalidOperationException()
        {
            // Arrange
            string response = @"{invalid json syntax}";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AgentResponseParser.ParseErrors(response).ToList());
            Assert.That(ex?.Message, Does.Contain("Agent response is not in the expected JSON format"));
        }

        [Test]
        public void ParseErrors_WithCaseInsensitiveMarkdown_ParsesCorrectly()
        {
            // Arrange
            string response = @"```JSON
{""errors"": [{""type"": ""CaseTest"", ""message"": ""Case insensitive test""}]}
```";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("CaseTest"));
        }

        [Test]
        public void ParseErrors_WithIncompleteMarkdownBlock_ParsesCorrectly()
        {
            // Arrange
            string response = @"```json
{""errors"": [{""type"": ""IncompleteTest"", ""message"": ""Missing closing backticks""}]}";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("IncompleteTest"));
        }

        [Test]
        public void ParseErrors_WithGenericMarkdownBlock_ParsesCorrectly()
        {
            // Arrange
            string response = @"```
{""errors"": [{""type"": ""GenericTest"", ""message"": ""Generic markdown block""}]}
```";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("GenericTest"));
        }

        [Test]
        public void ParseErrors_WithMultipleMarkdownBlocks_UsesFirstBlock()
        {
            // Arrange
            string response = @"```json
{""errors"": [{""type"": ""FirstBlock"", ""message"": ""First block""}]}
```
Some text in between
```json
{""errors"": [{""type"": ""SecondBlock"", ""message"": ""Second block""}]}
```";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("FirstBlock"));
        }

        [Test]
        public void ParseErrors_WithWhitespaceAroundMarkdown_ParsesCorrectly()
        {
            // Arrange
            string response = @"   ```json   

{
    ""errors"": [
        {""type"": ""WhitespaceTest"", ""message"": ""Whitespace handling""}
    ]
}

```   ";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("WhitespaceTest"));
        }

        [Test]
        public void ParseErrors_WithTrailingCommas_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""errors"": [
                    {""type"": ""TrailingCommaTest"", ""message"": ""Test message"",},
                ],
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("TrailingCommaTest"));
        }

        [Test]
        public void ParseErrors_WithCaseInsensitiveProperties_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""ERRORS"": [
                    {""TYPE"": ""CaseInsensitiveTest"", ""MESSAGE"": ""Case insensitive properties""}
                ]
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("CaseInsensitiveTest"));
            Assert.That(result[0].message, Is.EqualTo("Case insensitive properties"));
        }

        [Test]
        public void ParseErrors_WithMixedCaseProperties_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""Errors"": [
                    {""Type"": ""MixedCaseTest"", ""Message"": ""Mixed case properties""}
                ]
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("MixedCaseTest"));
            Assert.That(result[0].message, Is.EqualTo("Mixed case properties"));
        }

        [Test]
        public void ParseErrors_WithSpecialCharactersInErrorMessage_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""errors"": [
                    {""type"": ""SpecialCharTest"", ""message"": ""Error with symbols: !@#$%^&*()[]{}|\\:;\"",.<>?/~`""}
                ]
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("SpecialCharTest"));
            Assert.That(result[0].message, Does.Contain("!@#$%^&*()"));
        }

        [Test]
        public void ParseErrors_WithEmptyErrorTypeAndMessage_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""errors"": [
                    {""type"": """", ""message"": """"}
                ]
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("UnspecifiedError"));
            Assert.That(result[0].message, Is.EqualTo("No message provided"));
        }

        [Test]
        public void ParseErrors_WithUnicodeCharacters_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""errors"": [
                    {""type"": ""UnicodeTest"", ""message"": ""Unicode:""}
                ]
            }";

            // Act
            var result = AgentResponseParser.ParseErrors(response).ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("UnicodeTest"));
            Assert.That(result[0].message, Does.Contain("Unicode:"));
        }

        [Test]
        public void ParsePatchRequest_WithNullParameter_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                AgentResponseParser.ParsePatchRequest(null!));
        }

        [Test]
        public void ParsePatchRequest_WithEmptyString_ThrowsJsonException()
        {
            // Act & Assert
            var ex = Assert.Throws<JsonException>(() =>
                AgentResponseParser.ParsePatchRequest(string.Empty));
        }

        [Test]
        public void ParsePatchRequest_WithWhitespaceOnly_ThrowsJsonException()
        {
            // Act & Assert
            var ex = Assert.Throws<JsonException>(() =>
                AgentResponseParser.ParsePatchRequest("   \n\t  "));
        }

        [Test]
        public void ParsePatchRequest_WithPlainTextAndJson_ParsesCorrectly()
        {
            // Arrange
            string response = @"here is the patch {""file"": ""test.tsp"", ""from_version"": 1, ""reason"": ""test"", ""changes"": []}";

            // Act
            var result = AgentResponseParser.ParsePatchRequest(response);

            // Assert
            Assert.That(result.File, Is.EqualTo("test.tsp"));
            Assert.That(result.FromVersion, Is.EqualTo(1));
            Assert.That(result.Reason, Is.EqualTo("test"));
            Assert.That(result.Changes, Is.Empty);
        }

        [Test]
        public void ParsePatchRequest_WithTextAndMarkdownJsonBlock_ParsesCorrectly()
        {
            // Arrange
            string response = @"here is the patch ```json
{
    ""file"": ""markdown-test.tsp"",
    ""from_version"": 1,
    ""reason"": ""Test markdown parsing"",
    ""changes"": [
        {
            ""type"": ""modify"",
            ""start_line"": 1,
            ""end_line"": 1,
            ""old_content"": ""old"",
            ""new_content"": ""new""
        }
    ]
}
```";

            // Act
            var result = AgentResponseParser.ParsePatchRequest(response);

            // Assert
            Assert.That(result.File, Is.EqualTo("markdown-test.tsp"));
            Assert.That(result.FromVersion, Is.EqualTo(1));
            Assert.That(result.Reason, Is.EqualTo("Test markdown parsing"));
            Assert.That(result.Changes, Has.Count.EqualTo(1));
        }

        [Test]
        public void ParsePatchRequest_WithValidJsonResponse_ReturnsCorrectPatchRequest()
        {
            // Arrange
            string validJsonResponse = @"{
                ""file"": ""test-file.tsp"",
                ""from_version"": 1,
                ""reason"": ""Fix validation error"",
                ""changes"": [
                    {
                        ""type"": ""modify"",
                        ""start_line"": 10,
                        ""end_line"": 12,
                        ""old_content"": ""old content"",
                        ""new_content"": ""new content""
                    }
                ]
            }";

            // Act
            var result = AgentResponseParser.ParsePatchRequest(validJsonResponse);

            // Assert
            Assert.That(result.File, Is.EqualTo("test-file.tsp"));
            Assert.That(result.FromVersion, Is.EqualTo(1));
            Assert.That(result.Reason, Is.EqualTo("Fix validation error"));
            Assert.That(result.Changes, Has.Count.EqualTo(1));
            Assert.That(result.Changes[0].Type, Is.EqualTo("modify"));
            Assert.That(result.Changes[0].StartLine, Is.EqualTo(10));
            Assert.That(result.Changes[0].EndLine, Is.EqualTo(12));
            Assert.That(result.Changes[0].OldContent, Is.EqualTo("old content"));
            Assert.That(result.Changes[0].NewContent, Is.EqualTo("new content"));
        }

        [Test]
        public void ParsePatchRequest_WithMultipleChanges_ReturnsCorrectPatchRequest()
        {
            // Arrange
            string jsonWithMultipleChanges = @"{
                ""file"": ""multi-change.tsp"",
                ""from_version"": 2,
                ""reason"": ""Multiple fixes"",
                ""changes"": [
                    {
                        ""type"": ""add"",
                        ""start_line"": 5,
                        ""end_line"": 5,
                        ""old_content"": """",
                        ""new_content"": ""new line""
                    },
                    {
                        ""type"": ""delete"",
                        ""start_line"": 20,
                        ""end_line"": 22,
                        ""old_content"": ""to be deleted"",
                        ""new_content"": """"
                    }
                ]
            }";

            // Act
            var result = AgentResponseParser.ParsePatchRequest(jsonWithMultipleChanges);

            // Assert
            Assert.That(result.File, Is.EqualTo("multi-change.tsp"));
            Assert.That(result.FromVersion, Is.EqualTo(2));
            Assert.That(result.Reason, Is.EqualTo("Multiple fixes"));
            Assert.That(result.Changes, Has.Count.EqualTo(2));
            Assert.That(result.Changes[0].Type, Is.EqualTo("add"));
            Assert.That(result.Changes[1].Type, Is.EqualTo("delete"));
        }

        [Test]
        public void ParsePatchRequest_WithInvalidJsonSyntax_ThrowsJsonException()
        {
            // Arrange
            string invalidJson = @"{invalid json syntax}";

            // Act & Assert
            var ex = Assert.Throws<JsonException>(() =>
                AgentResponseParser.ParsePatchRequest(invalidJson));
        }

        [Test]
        public void ParsePatchRequest_WithTrailingCommas_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""file"": ""trailing-comma-test.tsp"",
                ""from_version"": 1,
                ""reason"": ""Test trailing commas"",
                ""changes"": [
                    {
                        ""type"": ""modify"",
                        ""start_line"": 1,
                        ""end_line"": 1,
                        ""old_content"": ""old"",
                        ""new_content"": ""new"",
                    },
                ],
            }";

            // Act
            var result = AgentResponseParser.ParsePatchRequest(response);

            // Assert
            Assert.That(result.File, Is.EqualTo("trailing-comma-test.tsp"));
            Assert.That(result.FromVersion, Is.EqualTo(1));
            Assert.That(result.Changes, Has.Count.EqualTo(1));
        }

        [Test]
        public void ParsePatchRequest_WithCaseInsensitiveProperties_ParsesCorrectly()
        {
            // Arrange
            string response = @"{
                ""FILE"": ""case-test.tsp"",
                ""FROM_VERSION"": 1,
                ""REASON"": ""Case insensitive test"",
                ""CHANGES"": [
                    {
                        ""TYPE"": ""modify"",
                        ""START_LINE"": 1,
                        ""END_LINE"": 1,
                        ""OLD_CONTENT"": ""old"",
                        ""NEW_CONTENT"": ""new""
                    }
                ]
            }";

            // Act
            var result = AgentResponseParser.ParsePatchRequest(response);

            // Assert
            Assert.That(result.File, Is.EqualTo("case-test.tsp"));
            Assert.That(result.FromVersion, Is.EqualTo(1));
            Assert.That(result.Reason, Is.EqualTo("Case insensitive test"));
            Assert.That(result.Changes, Has.Count.EqualTo(1));
        }
    }
}
