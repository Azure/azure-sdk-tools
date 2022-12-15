// --------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// The MIT License (MIT)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the ""Software""), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//
// --------------------------------------------------------------------------

import Foundation
import SwiftSyntax


class PackageModel: Tokenizable, Linkable {

    let name: String
    var definitionId: String?
    var parent: Linkable?
    var statements: [CodeBlockItemSyntax.Item]

    init(name: String, statements: [CodeBlockItemSyntax.Item]) {
        self.name = name
        self.definitionId = name
        self.parent = nil
        self.statements = statements
    }

    private func tokenize(statements: [CodeBlockItemSyntax.Item], apiview a: APIViewModel) {
        for statement in statements {
            switch statement {
            case let .decl(decl):
                switch decl.kind {
                case .actorDecl: fallthrough
                case .classDecl: fallthrough
                case .deinitializerDecl: fallthrough
                case .enumDecl: fallthrough
                case .extensionDecl: fallthrough
                case .functionDecl: fallthrough
                case .initializerDecl: fallthrough
                case .operatorDecl: fallthrough
                case .precedenceGroupDecl: fallthrough
                case .protocolDecl: fallthrough
                case .structDecl: fallthrough
                case .subscriptDecl: fallthrough
                case .typealiasDecl: fallthrough
                case .variableDecl:
                    // TODO: Fix how extensions are handled
                    for element in statement.children(viewMode: .sourceAccurate) {
                        self.tokenize(element: element, apiview: a)
                    }
                case .importDecl:
                    // purposely ignore import declarations
                    break
                default:
                    SharedLogger.warn("Unsupported declaration type: \(decl.kind)")
                }
                break
            default:
                SharedLogger.warn("Unsupported statement type: \(statement.kind)")
            }
            a.blankLines(set: 0)
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.text("package")
        a.whitespace()
        a.text(name, definitionId: definitionId)
        a.punctuation("{", spacing: SwiftSyntax.TokenKind.leftBrace.spacing)
        a.newline()
        a.indent {
            self.tokenize(statements: statements, apiview: a)
        }
        a.punctuation("}", spacing: SwiftSyntax.TokenKind.rightBrace.spacing)
        a.newline()
    }

    func tokenize(element: SyntaxProtocol, apiview a: APIViewModel) {
        switch element.kind {
        case .token:
            let item = TokenSyntax(element)!
            let tokenKind = item.tokenKind

            if tokenKind.isKeyword {
                a.keyword(item.withoutTrivia().description, spacing: tokenKind.spacing)
                return
            } else if tokenKind.isPunctuation {
                let punct = item.withoutTrivia().description
                a.punctuation(punct, spacing: tokenKind.spacing)
                return
            }
            if case let SwiftSyntax.TokenKind.identifier(val) = tokenKind {
                // FIXME: should not be text... should be a member declaration or reference
                a.text(val)
            } else if case let SwiftSyntax.TokenKind.spacedBinaryOperator(val) = tokenKind {
                a.punctuation(val, spacing: .Both)
            } else if case let SwiftSyntax.TokenKind.unspacedBinaryOperator(val) = tokenKind {
                a.punctuation(val, spacing: .Neither)
            } else if case let SwiftSyntax.TokenKind.prefixOperator(val) = tokenKind {
                a.punctuation(val, spacing: .Leading)
            } else if case let SwiftSyntax.TokenKind.postfixOperator(val) = tokenKind {
                a.punctuation(val, spacing: .Trailing)
            } else if case let SwiftSyntax.TokenKind.floatingLiteral(val) = tokenKind {
                a.literal(val)
            } else if case let SwiftSyntax.TokenKind.regexLiteral(val) = tokenKind {
                a.stringLiteral(val)
            } else if case let SwiftSyntax.TokenKind.stringLiteral(val) = tokenKind {
                a.stringLiteral(val)
            } else if case let SwiftSyntax.TokenKind.integerLiteral(val) = tokenKind {
                a.literal(val)
            } else if case let SwiftSyntax.TokenKind.contextualKeyword(val) = tokenKind {
                a.keyword(val, spacing: tokenKind.spacing)
            } else if case let SwiftSyntax.TokenKind.stringSegment(val) = tokenKind {
                a.text(val)
            } else {
                SharedLogger.warn("Unsupported tokenKind \(tokenKind)")
                let unknownText = element.description
                a.text(unknownText)
            }
        case .memberDeclBlock:
            let item = MemberDeclBlockSyntax(element)!
            a.punctuation("{", spacing: SwiftSyntax.TokenKind.leftBrace.spacing)
            a.newline()
            a.indent {
                for child in item.members {
                    tokenize(element: child, apiview: a)
                    a.blankLines(set: 0)
                }
            }
            a.punctuation("}", spacing: SwiftSyntax.TokenKind.rightBrace.spacing)
        case .codeBlockItem:
            // Don't render code blocks
            break
        default:
            //SharedLogger.warn("Default for \(element.kind)")
            for child in element.children(viewMode: .sourceAccurate) {
                self.tokenize(element: child, apiview: a)
            }
        }
    }

    func navigationTokenize(apiview a: APIViewModel) {
        // TODO: rebuild!
//        for member in members {
//            guard let member = member as? Linkable else { continue }
//            member.navigationTokenize(apiview: a)
//        }
        // sort the root navigation elements by name
        a.navigation.sort { $0.name < $1.name }
    }
}
