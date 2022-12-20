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

extension SyntaxProtocol {
    func tokenize(apiview a: APIViewModel, parent: Linkable?) {
        if self.needsParent && parent == nil {
            SharedLogger.fail("\(self.kind) needs a parent")
        }
        switch self.kind {
        case .associatedtypeDecl:
            // TODO: Support this
            break
        case .attribute:
            for child in self.children(viewMode: .sourceAccurate) {
                child.tokenize(apiview: a, parent: parent)
            }
        case .attributeList:
            // FIXME: Address attribute lists, which could be line-by-line or inline
            break
//            for child in self.children(viewMode: .sourceAccurate) {
//                child.tokenize(apiview: a, parent: parent)
//                a.blankLines(set: 0)
//            }
        case .codeBlock:
            // FIXME: Does this also suppress get/set blocks?
            // Don't render code blocks. APIView is unconcerned with implementation
            break
        case .enumCaseDecl:
            // TODO: Support this
            break
        case .functionDecl:
            // TODO: implement this
            break
        case .functionParameter:
            let param = FunctionParameterSyntax(self)!
            for child in param.children(viewMode: .sourceAccurate) {
                let childNameInParent = child.childNameInParent
                // we do not want to render the internal name
                guard childNameInParent != "internal name" else { continue }
                child.tokenize(apiview: a, parent: parent)
            }
        case .functionSignature:
            // TODO: implement this
            break
        case .genericParameter:
            // TODO: Support this
            break
        case .genericParameterClause:
            // TODO: Support this
            break
        case .genericWhereClause:
            // TODO: Support this
            break
        case .initializerClause:
            // TODO: Support this
            break
        case .initializerDecl:
            // TODO: implement this
            break
        case .memberDeclBlock:
            let declBlock = MemberDeclBlockSyntax(self)!
            // don't render anything if there are no members
            // FIXME: This will need to account only for public members!
            guard declBlock.members.count > 0 else { break }
            for child in self.children(viewMode: .sourceAccurate) {
                child.tokenize(apiview: a, parent: parent)
            }
        case .memberDeclList:
            a.indent {
                for child in self.children(viewMode: .sourceAccurate) {
                    child.tokenize(apiview: a, parent: parent)
                }
                a.newline()
            }
        case .memberDeclListItem:
            a.newline()
            for child in self.children(viewMode: .sourceAccurate) {
                child.tokenize(apiview: a, parent: parent)
            }
        case .modifierList:
            // TODO: Support this
            break
        case .operatorPrecedenceAndTypes:
            // TODO: Support this
            break
        case .precedenceGroupAttributeList:
            // TODO: Support this
            break
        case .subscriptDecl:
            // TODO: Support this
            break
        case .token:
            let token = TokenSyntax(self)!
            tokenize(token: token, apiview: a)
        case .typealiasDecl:
            // TODO: Support this
            break
        case .typeInheritanceClause:
            // TODO: Support this
            break
        case .typeInitializerClause:
            // TODO: Support this
            break
        case .variableDecl:
            // TODO: implement this
            break
        default:
            // FIXME: Re-enable fallback once most things are supported again
            SharedLogger.warn("Skipping: \(self.kind)")
//            for child in self.children(viewMode: .sourceAccurate) {
//                child.tokenize(apiview: a, parent: parent)
//            }
        }
    }

    func tokenize(token: TokenSyntax, apiview a: APIViewModel) {
        let tokenKind = token.tokenKind
        let tokenText = token.withoutTrivia().description

        if tokenKind.isKeyword {
            a.keyword(tokenText, spacing: tokenKind.spacing)
            return
        } else if tokenKind.isPunctuation {
            a.punctuation(tokenText, spacing: tokenKind.spacing)
            return
        }
        if case let SwiftSyntax.TokenKind.identifier(val) = tokenKind {
            // FIXME: Need to distinguish between member names and referenced types...
            a.typeReference(name: val)
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
            SharedLogger.warn("Unsupported tokenKind \(tokenKind) = \(tokenText)")
            a.text(tokenText)
        }
    }

    var needsParent: Bool {
        switch self.kind {
        case .attributeList: return true
        case .memberDeclList: return true
        default: return false
        }
    }
}
