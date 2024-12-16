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
        var options = ReviewTokenOptions()
        let syntaxKind = self.kind
        switch syntaxKind {
        case .associatedtypeDecl:
            let decl = DeclarationModel(from: AssociatedtypeDeclSyntax(self)!, parent: parent)
            decl.tokenize(apiview: a, parent: parent)
        case .customAttribute: fallthrough
        case .attribute:
            // default implementation should not have newlines
            let children = self.children(viewMode: .sourceAccurate)
            for child in children {
                if child.childNameInParent == "name" {
                    let attrName = child.withoutTrivia().description
                    // don't add space if the attribute has parameters
                    if children.count == 2 {
                        options.applySpacing(.Trailing)
                        a.keyword(attrName, options: options)
                    } else {
                        options.applySpacing(.Neither)
                        a.keyword(attrName, options: options)
                    }

                } else {
                    child.tokenize(apiview: a, parent: parent)
                }
            }
        case .classRestrictionType:
            // in this simple context, class should not have a trailing space
            options.applySpacing(.Neither)
            a.keyword("class", options: options)
        case .codeBlock:
            // Don't render code blocks. APIView is unconcerned with implementation
            break
        case .constrainedSugarType:
            let obj = ConstrainedSugarTypeSyntax(self)!
            let children = obj.children(viewMode: .sourceAccurate)
            assert(children.count == 2)
            for child in children {
                child.tokenize(apiview: a, parent: parent)
                if (child.kind == .token) {
                    a.currentLine.tokens.last?.hasSuffixSpace = true
                }
            }
        case .enumCaseElement:
            for child in self.children(viewMode: .sourceAccurate) {
                let childIndex = child.indexInParent
                // index 1 is the enum case label. It must be rendered as a member
                if childIndex == 1 {
                    let token = TokenSyntax(child)!
                    if case let SwiftSyntax.TokenKind.identifier(label) = token.tokenKind {
                        let lineId = identifier(forName: label, withPrefix: parent?.definitionId)
                        a.lineMarker(lineId)
                        a.member(name: label)
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
                let childKind = child.kind
                let childIndex = child.indexInParent
                // index 7 is the interal name, which we don't render at all
                guard childIndex != 7 else { continue }
                if childIndex == 5 {
                    // index 5 is the external name, which we always render as text
                    let token = TokenSyntax(child)!
                    if case let SwiftSyntax.TokenKind.identifier(val) = token.tokenKind {
                        options.hasSuffixSpace = false
                        a.text(val, options: options)
                    } else if case SwiftSyntax.TokenKind.wildcardKeyword = token.tokenKind {
                        a.text("_")
                    } else {
                        SharedLogger.warn("Unhandled tokenKind '\(token.tokenKind)' for function parameter label")
                    }
                } else if childKind == .attributeList {
                    let attrs = AttributeListSyntax(child)!
                    let lastAttrs = attrs.count - 1
                    for attr in attrs {
                        let attrIndex = attrs.indexInParent
                        attr.tokenize(apiview: a, parent: parent)
                        if attrIndex != lastAttrs {
                            a.currentLine.tokens.last?.hasSuffixSpace = true
                        }
                    }
                } else {
                    child.tokenize(apiview: a, parent: parent)
                }
            }
        case .identifierPattern:
            let name = IdentifierPatternSyntax(self)!.identifier.withoutTrivia().text
            let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
            a.lineMarker(lineId)
            a.member(name: name)
        case .initializerDecl:
            DeclarationModel(from: InitializerDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        case .memberDeclList:
            a.indent {
                tokenizeMembers(apiview: a, parent: parent)
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
                tokenizeChildren(apiview: a, parent: parent)
            }
        case .precedenceGroupAttributeList:
            a.indent {
                tokenizeChildren(apiview: a, parent: parent)
                // FIXME: Newline
                //a.newline()
            }
        case .precedenceGroupRelation:
            // FIXME: Newline
            //a.newline()
            if let name = PrecedenceGroupRelationSyntax(self)!.keyword {
                let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
                a.lineMarker(lineId)
            }
            tokenizeChildren(apiview: a, parent: parent)
        case .precedenceGroupAssociativity:
            // FIXME: Newline
            //a.newline()
            if let name = PrecedenceGroupAssociativitySyntax(self)!.keyword {
                let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
                a.lineMarker(lineId)
            }
            tokenizeChildren(apiview: a, parent: parent)
        case .subscriptDecl:
            DeclarationModel(from: SubscriptDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        case .token:
            let token = TokenSyntax(self)!
            tokenize(token: token, apiview: a, parent: (parent as? DeclarationModel))
        case .typealiasDecl:
            DeclarationModel(from: TypealiasDeclSyntax(self)!, parent: parent).tokenize(apiview: a, parent: parent)
        case .accessorBlock:
            let obj = AccessorBlockSyntax(self)!
            for child in obj.children(viewMode: .sourceAccurate) {
                if child.kind == .token {
                    let token = TokenSyntax(child)!
                    let tokenKind = token.tokenKind
                    let tokenText = token.withoutTrivia().description
                    if tokenKind == .leftBrace || tokenKind == .rightBrace {
                        options.applySpacing(tokenKind.spacing)
                        a.punctuation(tokenText, options: options)
                    } else {
                        child.tokenize(token: token, apiview: a, parent: nil)
                    }
                } else {
                    child.tokenize(apiview: a, parent: parent)
                }
            }
        default:
            // default behavior for all nodes is to render all children
            tokenizeChildren(apiview: a, parent: parent)
        }
    }

    func tokenizeMembers(apiview a: APIViewModel, parent: Linkable?) {
        let children = self.children(viewMode: .sourceAccurate)
        let lastIdx = children.count - 1
        for (idx, child) in children.enumerated() {
            let beforeCount = a.currentLine.tokens.count
            child.tokenize(apiview: a, parent: parent)
            // skip if no tokens were actually added
            guard (a.currentLine.tokens.count > beforeCount) else { continue }
            // FIXME: Only add a blank line between members if there's attributes
            // add 1 blank line, except for the last member
            a.blankLines(set: idx != lastIdx ? 0 : 0)
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
        var options = ReviewTokenOptions()
        options.applySpacing(tokenKind.spacing)

        if tokenKind.isKeyword {
            a.keyword(tokenText, options: options)
            return
        } else if tokenKind.isPunctuation {
            a.punctuation(tokenText, options: options)
            return
        }
        if case let SwiftSyntax.TokenKind.identifier(val) = tokenKind {
            let nameInParent = self.childNameInParent
            // used in @availabililty annotations
            if nameInParent == "platform" {
                a.text(tokenText)
                a.currentLine.tokens.last?.hasSuffixSpace = true
            } else {
                a.typeReference(name: val, options: options)
            }
        } else if case let SwiftSyntax.TokenKind.spacedBinaryOperator(val) = tokenKind {
            // in APIView, * is never used for multiplication
            if val == "*" {
                options.applySpacing(.Neither)
                a.punctuation(val, options: options)
            } else {
                options.applySpacing(.Both)
                a.punctuation(val, options: options)
            }
        } else if case let SwiftSyntax.TokenKind.unspacedBinaryOperator(val) = tokenKind {
            options.applySpacing(.Neither)
            a.punctuation(val, options: options)
        } else if case let SwiftSyntax.TokenKind.prefixOperator(val) = tokenKind {
            options.applySpacing(.Leading)
            a.punctuation(val, options: options)
        } else if case let SwiftSyntax.TokenKind.postfixOperator(val) = tokenKind {
            options.applySpacing(.Trailing)
            a.punctuation(val, options: options)
        } else if case let SwiftSyntax.TokenKind.floatingLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.regexLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.stringLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.integerLiteral(val) = tokenKind {
            a.literal(val)
        } else if case let SwiftSyntax.TokenKind.contextualKeyword(val) = tokenKind {
            options.applySpacing(tokenKind.spacing)
            a.keyword(val, options: options)
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
