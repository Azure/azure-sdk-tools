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
        let syntaxKind = self.kind
        switch syntaxKind {
        case .associatedtypeDecl:
            let decl = DeclarationModel(from: AssociatedtypeDeclSyntax(self)!, parent: parent)
            decl.tokenize(apiview: a, parent: parent)
        case .customAttribute: fallthrough
        case .attribute:
            // default implementation should not have newlines
            for child in self.children(viewMode: .sourceAccurate) {
                if child.childNameInParent == "name" {
                    let attrName = child.withoutTrivia().description
                    a.keyword(attrName, spacing: .Neither)
                } else {
                    child.tokenize(apiview: a, parent: parent)
                }
            }
            a.whitespace()
        case .classRestrictionType:
            // in this simple context, class should not have a trailing space
            a.keyword("class", spacing: .Neither)
        case .codeBlock:
            // Don't render code blocks. APIView is unconcerned with implementation
            break
        case .enumCaseElement:
            for child in self.children(viewMode: .sourceAccurate) {
                let childIndex = child.indexInParent
                // index 1 is the enum case label. It must be rendered as a member
                if childIndex == 1 {
                    let token = TokenSyntax(child)!
                    if case let SwiftSyntax.TokenKind.identifier(label) = token.tokenKind {
                        let defId = identifier(forName: label, withPrefix: parent?.definitionId)
                        a.member(name: label, definitionId: defId)
                    } else {
                        SharedLogger.warn("Unhandled enum label kind '\(token.tokenKind)'. APIView may not display correctly.")
                    }
                } else {
                    child.tokenize(apiview: a, parent: parent)
                }
            }
        case .enumDecl:
            DeclarationModel(from: EnumDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        case .functionDecl:
            DeclarationModel(from: FunctionDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        case .functionParameter:
            let param = FunctionParameterSyntax(self)!
            for child in param.children(viewMode: .sourceAccurate) {
                let childIndex = child.indexInParent
                // index 7 is the interal name, which we don't render at all
                guard childIndex != 7 else { continue }
                if childIndex == 5 {
                    // index 5 is the external name, which we always render as text
                    let token = TokenSyntax(child)!
                    if case let SwiftSyntax.TokenKind.identifier(val) = token.tokenKind {
                        a.text(val)
                    } else if case SwiftSyntax.TokenKind.wildcardKeyword = token.tokenKind {
                        a.text("_")
                    } else {
                        SharedLogger.warn("Unhandled tokenKind '\(token.tokenKind)' for function parameter label")
                    }
                } else {
                    child.tokenize(apiview: a, parent: parent)
                }
            }
        case .identifierPattern:
            let name = IdentifierPatternSyntax(self)!.identifier.withoutTrivia().text
            let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
            a.member(name: name, definitionId: lineId)
        case .initializerDecl:
            DeclarationModel(from: InitializerDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
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
            let decl = MemberDeclListItemSyntax(self)!.decl
            let publicModifiers = APIViewModel.publicModifiers
            var showDecl = publicModifiers.contains(decl.modifiers?.accessLevel ?? .unspecified)
            switch decl.kind {
            case .associatedtypeDecl:
                // all associated types are public
                showDecl = true
            case .deinitializerDecl:
                // deinitializers can't be called directly and don't need to be shown as public API
                showDecl = false
            case .enumCaseDecl:
                // all enum cases are public
                showDecl = true
            case .enumDecl: fallthrough
            case .functionDecl: fallthrough
            case .initializerDecl: fallthrough
            case .subscriptDecl: fallthrough
            case .typealiasDecl: fallthrough
            case .variableDecl:
                // Public protocols should expose all members even if they have no access level modifier
                if let parentDecl = (parent as? DeclarationModel), parentDecl.kind == .protocol {
                    showDecl = showDecl || APIViewModel.publicModifiers.contains(parentDecl.accessLevel)
                }
            default:
                // show the unrecognized member by default
                SharedLogger.warn("Unhandled member declaration type '\(decl.kind)'. This may not display correctly in APIView.")
                showDecl = true
            }
            if showDecl {
                a.newline()
                tokenizeChildren(apiview: a, parent: parent)
            }
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
        case .precedenceGroupAssociativity:
            a.newline()
            if let name = PrecedenceGroupAssociativitySyntax(self)!.keyword {
                let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
                a.lineIdMarker(definitionId: lineId)
            }
            tokenizeChildren(apiview: a, parent: parent)
        case .subscriptDecl:
            DeclarationModel(from: SubscriptDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        case .token:
            let token = TokenSyntax(self)!
            tokenize(token: token, apiview: a, parent: (parent as? DeclarationModel))
        case .typealiasDecl:
            DeclarationModel(from: TypealiasDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        default:
            // default behavior for all nodes is to render all children
            tokenizeChildren(apiview: a, parent: parent)
        }
    }

    func tokenizeChildren(apiview a: APIViewModel, parent: Linkable?) {
        for child in self.children(viewMode: .sourceAccurate) {
            child.tokenize(apiview: a, parent: parent)
        }
    }

    func tokenize(token: TokenSyntax, apiview a: APIViewModel, parent: DeclarationModel?) {
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
            let nameInParent = self.childNameInParent
            // used in @availabililty annotations
            if nameInParent == "platform" {
                a.text(tokenText)
                a.whitespace()
            } else {
                a.typeReference(name: val, parent: parent)
            }
        } else if case let SwiftSyntax.TokenKind.spacedBinaryOperator(val) = tokenKind {
            // in APIView, * is never used for multiplication
            if val == "*" {
                a.punctuation(val, spacing: .Neither)
            } else {
                a.punctuation(val, spacing: .Both)
            }
        } else if case let SwiftSyntax.TokenKind.unspacedBinaryOperator(val) = tokenKind {
            a.punctuation(val, spacing: .Neither)
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

extension DeclSyntax {
    var modifiers: ModifierListSyntax? {
        switch self.kind {
        case .associatedtypeDecl:
            return AssociatedtypeDeclSyntax(self)!.modifiers
        case .enumCaseDecl:
            // enum cases don't have modifiers
            return nil
        case .enumDecl:
            return EnumDeclSyntax(self)!.modifiers
        case .functionDecl:
            return FunctionDeclSyntax(self)!.modifiers
        case .initializerDecl:
            return InitializerDeclSyntax(self)!.modifiers
        case .subscriptDecl:
            return SubscriptDeclSyntax(self)!.modifiers
        case .typealiasDecl:
            return TypealiasDeclSyntax(self)!.modifiers
        case .variableDecl:
            return VariableDeclSyntax(self)!.modifiers
        case .deinitializerDecl:
            // deinitializers can never be called directly anyhow so this
            // is irrelevant
            return nil
        default:
            SharedLogger.warn("Unhandled syntax type when looking for modifiers: \(self.kind)")
            return nil
        }
    }
}
