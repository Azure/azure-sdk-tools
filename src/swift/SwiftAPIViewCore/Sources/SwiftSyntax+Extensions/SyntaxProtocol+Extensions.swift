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
            // FIXME: needs to be commentable and linkable
            tokenizeChildren(apiview: a, parent: parent)
        case .attribute:
            // FIXME: needs to be commentable
            tokenizeChildren(apiview: a, parent: parent)
            a.newline()
        case .attributeList:
            tokenizeChildren(apiview: a, parent: parent)
        case .availabilityArgument:
            tokenizeChildren(apiview: a, parent: parent)
        case .availabilityLabeledArgument:
            tokenizeChildren(apiview: a, parent: parent)
        case .availabilitySpecList:
            tokenizeChildren(apiview: a, parent: parent)
        case .availabilityVersionRestriction:
            tokenizeChildren(apiview: a, parent: parent)
        case .codeBlock:
            // FIXME: Does this also suppress get/set blocks?
            // Don't render code blocks. APIView is unconcerned with implementation
            break
        case .customAttribute:
            // TODO: support this
            break
        case .declModifier:
            tokenizeChildren(apiview: a, parent: parent)
        case .designatedTypeList:
            tokenizeChildren(apiview: a, parent: parent)
        case .enumCaseDecl:
            tokenizeChildren(apiview: a, parent: parent)
        case .enumCaseElement:
            // FIXME: render token as member and make line commentable
            tokenizeChildren(apiview: a, parent: parent)
        case .enumCaseElementList:
            // FIXME: render tokens as members and make line commentable
            // Arguably, make each case on a separate line, regardless of how it is in the code
            tokenizeChildren(apiview: a, parent: parent)
        case .functionDecl:
            DeclarationModel(from: FunctionDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .functionParameter:
            let param = FunctionParameterSyntax(self)!
            for child in param.children(viewMode: .sourceAccurate) {
                let childNameInParent = child.childNameInParent
                // we do not want to render the internal name
                guard childNameInParent != "internal name" else { continue }
                child.tokenize(apiview: a, parent: parent)
            }
        case .functionParameterList:
            tokenizeChildren(apiview: a, parent: parent)
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
            DeclarationModel(from: InitializerDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .memberDeclBlock:
            let declBlock = MemberDeclBlockSyntax(self)!
            // don't render anything if there are no members
            // FIXME: This will need to account only for public members!
            guard declBlock.members.count > 0 else { break }
            tokenizeChildren(apiview: a, parent: parent)
        case .memberDeclList:
            a.indent {
                let beforeCount = a.tokens.count
                tokenizeChildren(apiview: a, parent: parent)
                // only render newline if tokens were actually added
                if a.tokens.count > beforeCount {
                    a.newline()
                }
            }
        case .memberDeclListItem:
            a.newline()
            tokenizeChildren(apiview: a, parent: parent)
        case .modifierList:
            tokenizeChildren(apiview: a, parent: parent)
        case .operatorPrecedenceAndTypes:
            tokenizeChildren(apiview: a, parent: parent)
        case .parameterClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .precedenceGroupAttributeList:
            a.indent {
                tokenizeChildren(apiview: a, parent: parent)
                a.newline()
            }
        case .precedenceGroupRelation:
            a.newline()
            if let name = PrecedenceGroupRelationSyntax(self)!.keyword {
                let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
                a.lineIdMarker(definitionId: lineId)
            }
            tokenizeChildren(apiview: a, parent: parent)
        case .precedenceGroupNameList, .precedenceGroupNameElement:
            tokenizeChildren(apiview: a, parent: parent)
        case .precedenceGroupAssociativity:
            a.newline()
            if let name = PrecedenceGroupAssociativitySyntax(self)!.keyword {
                let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
                a.lineIdMarker(definitionId: lineId)
            }
            tokenizeChildren(apiview: a, parent: parent)
        case .returnClause:
            // TODO: Support this
            break
        case .simpleTypeIdentifier:
            tokenizeChildren(apiview: a, parent: parent)
        case .subscriptDecl:
            DeclarationModel(from: SubscriptDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .token:
            let token = TokenSyntax(self)!
            tokenize(token: token, apiview: a)
        case .typealiasDecl:
            DeclarationModel(from: TypealiasDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .typeInheritanceClause:
            // TODO: Support this
            break
        case .typeInitializerClause:
            // TODO: Support this
            break
        case .variableDecl:
            // TODO: implement this
            break
        case .versionTuple:
            tokenizeChildren(apiview: a, parent: parent)
        default:
            SharedLogger.warn("No implmentation for syntax kind: \(self.kind)")
            tokenizeChildren(apiview: a, parent: parent)
        }
    }

    func tokenizeChildren(apiview a: APIViewModel, parent: Linkable?) {
        for child in self.children(viewMode: .sourceAccurate) {
            child.tokenize(apiview: a, parent: parent)
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
            // FIXME: Need to distinguish between member names, declarations and referenced types...
            let nameInParent = self.childNameInParent
            if nameInParent == "platform" {
                a.text(tokenText)
                a.whitespace()
            } else {
                // FIXME: Associatedvalues needs to do a type declaration here...
                a.typeReference(name: val)
            }
        } else if case let SwiftSyntax.TokenKind.spacedBinaryOperator(val) = tokenKind {
            a.punctuation(val, spacing: .Neither)
        } else if case let SwiftSyntax.TokenKind.unspacedBinaryOperator(val) = tokenKind {
            a.punctuation(val, spacing: .Both)
        } else if case let SwiftSyntax.TokenKind.prefixOperator(val) = tokenKind {
            a.punctuation(val, spacing: .Leading)
        } else if case let SwiftSyntax.TokenKind.postfixOperator(val) = tokenKind {
            a.punctuation(val, spacing: .Trailing)
        } else if case let SwiftSyntax.TokenKind.floatingLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.regexLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.stringLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.integerLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.contextualKeyword(val) = tokenKind {
            a.keyword(val, spacing: tokenKind.spacing)
        } else if case let SwiftSyntax.TokenKind.stringSegment(val) = tokenKind {
            a.text(val)
        } else {
            SharedLogger.warn("No implementation for token kind: \(tokenKind)")
            a.text(tokenText)
        }
    }

    var needsParent: Bool {
        switch self.kind {
        case .attributeList: return true
        case .memberDeclList: return true
        case .enumCaseDecl: return true
        default: return false
        }
    }
}

extension PrecedenceGroupRelationSyntax {
    var keyword: String? {
        switch self.higherThanOrLowerThan.tokenKind {
        case let .contextualKeyword(val): return val
        default: return nil
        }
    }
}

extension PrecedenceGroupAssociativitySyntax {
    var keyword: String? {
        switch self.associativityKeyword.tokenKind {
        case let .contextualKeyword(val): return val
        default: return nil
        }
    }
}
