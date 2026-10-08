// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIView;
using APIView.Model.V2;
using APIView.TreeToken;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CppTreeFormatTests
{
    private static CodeFileManager CreateManager() => new(
        new LanguageService[] { new CppLanguageService(), new JsonLanguageService() },
        Mock.Of<IBlobCodeFileRepository>(),
        Mock.Of<IBlobOriginalsRepository>(),
        Mock.Of<IDevopsArtifactRepository>(),
        Mock.Of<ILogger<CodeFileManager>>());

    private static CodeFile TreeFile(string apiName = "Widget", string documentation = "Documentation",
        string source = "source.hpp:10") => new()
    {
        Language = "C++",
        ParserVersion = "1.0.0",
        ReviewLines =
        [
            new ReviewLine { Tokens = [new ReviewToken(source, TokenKind.Comment) { SkipDiff = true }] },
            new ReviewLine { Tokens = [new ReviewToken(documentation, TokenKind.Comment) { IsDocumentation = true }] },
            new ReviewLine
            {
                LineId = "Azure::Widget",
                Tokens = [new ReviewToken(apiName, TokenKind.TypeName) { HasSuffixSpace = false }],
                Children =
                [
                    new ReviewLine
                    {
                        LineId = "Azure::Widget::GetValue",
                        Tokens = [new ReviewToken("int GetValue(int input) const;", TokenKind.Text) { HasSuffixSpace = false }]
                    }
                ]
            }
        ]
    };

    [Fact]
    public async Task CppTreeHashAndComparison_UseUploadedSchemaNotLanguageDefault()
    {
        Assert.False(new CppLanguageService().UsesTreeStyleParser);
        var manager = CreateManager();
        var original = TreeFile();
        var changed = TreeFile("OtherWidget");
        Assert.NotEqual(await manager.ComputeAPIContentHashAsync(original),
            await manager.ComputeAPIContentHashAsync(changed));
        Assert.False(manager.AreAPICodeFilesTheSame(new RenderedCodeFile(original), new RenderedCodeFile(changed)));
        changed = TreeFile();
        changed.ReviewLines[2].Children[0].Tokens[0].Value = "void ChangedMethod();";
        Assert.NotEqual(await manager.ComputeAPIContentHashAsync(original),
            await manager.ComputeAPIContentHashAsync(changed));
    }

    [Fact]
    public async Task CppTreeHashAndComparison_IgnoreDocumentationAndSourceLocations()
    {
        var manager = CreateManager();
        var original = TreeFile();
        var changed = TreeFile(documentation: "Changed docs", source: "source.hpp:20");
        Assert.Equal(await manager.ComputeAPIContentHashAsync(original),
            await manager.ComputeAPIContentHashAsync(changed));
        Assert.True(manager.AreAPICodeFilesTheSame(new RenderedCodeFile(original), new RenderedCodeFile(changed)));
    }

    [Fact]
    public async Task CppLegacyHashAndComparison_RemainContentSensitive()
    {
        var original = new CodeFile
        {
            Language = "C++",
            Tokens =
            [
                new CodeFileToken("Widget", CodeFileTokenKind.TypeName) { DefinitionId = "Widget" },
                new CodeFileToken(null, CodeFileTokenKind.Newline)
            ]
        };
        var changed = new CodeFile
        {
            Language = "C++",
            Tokens =
            [
                new CodeFileToken("Other", CodeFileTokenKind.TypeName) { DefinitionId = "Widget" },
                new CodeFileToken(null, CodeFileTokenKind.Newline)
            ]
        };
        var manager = CreateManager();
        Assert.NotEqual(await manager.ComputeAPIContentHashAsync(original),
            await manager.ComputeAPIContentHashAsync(changed));
        Assert.False(manager.AreAPICodeFilesTheSame(new RenderedCodeFile(original), new RenderedCodeFile(changed)));
        original.ParserVersion = "1.0.0";
        Assert.False(manager.AreAPICodeFilesTheSame(new RenderedCodeFile(original), new RenderedCodeFile(TreeFile())));
    }

    [Theory]
    [InlineData(ParserStyle.Flat, "/Assemblies/Review/")]
    [InlineData(ParserStyle.Tree, "/spa/browser/review/")]
    public void CppReviewUrl_UsesStoredRevisionStyle(ParserStyle style, string path)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["APIVIew-Host-Url"] = "http://localhost:5000",
            ["APIVIew-SPA-Host-Url"] = "http://localhost:5000/spa/browser"
        }).Build();
        var url = ManagerHelpers.ResolveReviewUrl("review", "revision", "C++", configuration,
            new[] { new CppLanguageService() }, parserStyle: style);
        Assert.Contains(path, url);
    }

    [Theory]
    [InlineData("https://example.test/header.hpp#L10")]
    [InlineData("http://example.test/docs")]
    public void ExternalLink_PreservesDisplayTextAndTarget(string url)
    {
        var token = new StructuredToken(new ReviewToken("header.hpp:10", TokenKind.ExternalUrl)
        {
            NavigateToId = url
        });
        Assert.Equal("header.hpp:10", token.Value);
        Assert.Equal(url, token.Properties["NavigateToUrl"]);
        Assert.False(token.Properties.ContainsKey("NavigateToId"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/private.txt")]
    [InlineData("data:text/html,unsafe")]
    public void ExternalLink_RejectsUnsafeSchemes(string url)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new StructuredToken(new ReviewToken(url, TokenKind.ExternalUrl)));
    }
}
