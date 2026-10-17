using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAPIParserTests
{
    internal class TestData
    {
        public static string TokenJsonSchema = @"{
    ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
    ""$id"": ""CodeFile.json"",
    ""type"": ""object"",
    ""properties"": {
        ""PackageName"": {
            ""type"": ""string""
        },
        ""PackageVersion"": {
            ""type"": ""string""
        },
        ""ParserVersion"": {
            ""type"": ""string"",
            ""description"": ""version of the APIview language parser used to create token file""
        },
        ""Language"": {
            ""anyOf"": [
                {
                    ""type"": ""string"",
                    ""const"": ""C""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""C++""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""C#""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Go""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Java""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""JavaScript""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Kotlin""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Python""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Swagger""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Swift""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""TypeSpec""
                }
            ]
        },
        ""LanguageVariant"": {
            ""anyOf"": [
                {
                    ""type"": ""string"",
                    ""const"": ""None""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Spring""
                },
                {
                    ""type"": ""string"",
                    ""const"": ""Android""
                }
            ],
            ""default"": ""None"",
            ""description"": ""Language variant is applicable only for java variants""
        },
        ""CrossLanguagePackageId"": {
            ""type"": ""string""
        },
        ""ReviewLines"": {
            ""type"": ""array"",
            ""items"": {
                ""$ref"": ""#/$defs/ReviewLine""
            }
        },
        ""Diagnostics"": {
            ""type"": ""array"",
            ""items"": {
                ""$ref"": ""#/$defs/CodeDiagnostic""
            },
            ""description"": ""Add any system generated comments. Each comment is linked to review line ID""
        }
    },
    ""required"": [
        ""PackageName"",
        ""PackageVersion"",
        ""ParserVersion"",
        ""Language"",
        ""ReviewLines""
    ],
    ""description"": ""ReviewFile represents entire API review object. This will be processed to render review lines."",
    ""$defs"": {
        ""ReviewLine"": {
            ""type"": ""object"",
            ""properties"": {
                ""LineId"": {
                    ""type"": ""string"",
                    ""description"": ""lineId is only required if we need to support commenting on a line that contains this token. \nUsually code line for documentation or just punctuation is not required to have lineId. lineId should be a unique value within \nthe review token file to use it assign to review comments as well as navigation Id within the review page.\nfor e.g Azure.Core.HttpHeader.Common, azure.template.template_main""
                },
                ""CrossLanguageId"": {
                    ""type"": ""string""
                },
                ""Tokens"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""$ref"": ""#/$defs/ReviewToken""
                    },
                    ""description"": ""list of tokens that constructs a line in API review""
                },
                ""Children"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""$ref"": ""#/$defs/ReviewLine""
                    },
                    ""description"": ""Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of namespace(module) level code line. \nSimilarly all method level code lines are added as children of it's class code line.""
                },
                ""IsHidden"": {
                    ""type"": ""boolean"",
                    ""description"": ""Set current line as hidden code line by default. .NET has hidden APIs and architects does not want to see them by default.""
                }
            },
            ""required"": [
                ""Tokens""
            ],
            ""description"": ""ReviewLine object corresponds to each line displayed on API review. If an empty line is required then add a code line object without any token.""
        },
        ""CodeDiagnostic"": {
            ""type"": ""object"",
            ""properties"": {
                ""DiagnosticId"": {
                    ""type"": ""string""
                },
                ""TargetId"": {
                    ""type"": ""string"",
                    ""description"": ""Id of ReviewLine object where this diagnostic needs to be displayed(Options""
                },
                ""Text"": {
                    ""type"": ""string""
                },
                ""Level"": {
                    ""$ref"": ""#/$defs/CodeDiagnosticLevel""
                },
                ""HelpLinkUri"": {
                    ""type"": ""string""
                }
            },
            ""required"": [
                ""TargetId"",
                ""Text"",
                ""Level""
            ],
            ""description"": ""System comment object is to add system generated comment. It can be one of the 4 different types of system comments.""
        },
        ""ReviewToken"": {
            ""type"": ""object"",
            ""properties"": {
                ""Kind"": {
                    ""$ref"": ""#/$defs/TokenKind""
                },
                ""Value"": {
                    ""type"": ""string""
                },
                ""NavigationDisplayName"": {
                    ""type"": ""string"",
                    ""description"": ""NavigationDisplayName can be used set navigation dispaly name to be used in navigation tree.\nThis value will be used in the navigation if NavigationDisplayName is not set.""
                },
                ""NavigateToId"": {
                    ""type"": ""string"",
                    ""description"": ""navigateToId should be set if the underlying token is required to be displayed as HREF to another type within the review.\nFor e.g. a param type which is class name in the same package""
                },
                ""SkipDiff"": {
                    ""type"": ""boolean"",
                    ""default"": false,
                    ""description"": ""set skipDiff to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency versions \nare usually excluded when comparing two revisions to avoid reporting them as API changes""
                },
                ""IsDeprecated"": {
                    ""type"": ""boolean"",
                    ""default"": false,
                    ""description"": ""This is set if API is marked as deprecated""
                },
                ""HasSuffixSpace"": {
                    ""type"": ""boolean"",
                    ""default"": true,
                    ""description"": ""Set this to false if there is no suffix space required before next token. For e.g, punctuation right after method name""
                },
                ""IsDocumentation"": {
                    ""type"": ""boolean"",
                    ""default"": false,
                    ""description"": ""Set isDocumentation to true if current token is part of documentation""
                },
                ""RenderClasses"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""type"": ""string""
                    },
                    ""description"": ""Language specific style css class names""
                }
            },
            ""required"": [
                ""Kind"",
                ""Value""
            ],
            ""description"": ""Token corresponds to each component within a code line. A separate token is required for keyword, punctuation, type name, text etc.""
        },
        ""CodeDiagnosticLevel"": {
            ""type"": ""number"",
            ""enum"": [
                1,
                2,
                3,
                4
            ]
        },
        ""TokenKind"": {
            ""type"": ""number"",
            ""enum"": [
                0,
                1,
                2,
                3,
                4,
                5,
                6,
                7
            ]
        }
    }
}";
    }
}
