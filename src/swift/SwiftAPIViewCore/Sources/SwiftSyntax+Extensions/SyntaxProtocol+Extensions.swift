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
        case .accessorBlock:
            tokenizeChildren(apiview: a, parent: parent)
        case .accessorDecl:
            tokenizeChildren(apiview: a, parent: parent)
        case .accessorList:
            tokenizeChildren(apiview: a, parent: parent)
        case .accessorParameter:
            tokenizeChildren(apiview: a, parent: parent)
        case .arrayElement:
            tokenizeChildren(apiview: a, parent: parent)
        case .arrayElementList:
            tokenizeChildren(apiview: a, parent: parent)
        case .arrayExpr:
            tokenizeChildren(apiview: a, parent: parent)
        case .arrayType:
            tokenizeChildren(apiview: a, parent: parent)
        case .associatedtypeDecl:
            let decl = DeclarationModel(from: AssociatedtypeDeclSyntax(self)!, parent: parent)
            decl.tokenize(apiview: a, parent: parent)
        case .attribute:
            // default implementation should not have newlines
            tokenizeChildren(apiview: a, parent: parent)
        case .attributedType:
            tokenizeChildren(apiview: a, parent: parent)
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
        case .classRestrictionType:
            // in this simple context, class should not have a trailing space
            a.keyword("class", spacing: .Neither)
        case .codeBlock:
            // Don't render code blocks. APIView is unconcerned with implementation
            break
        case .compositionType:
            tokenizeChildren(apiview: a, parent: parent)
        case .compositionTypeElement:
            tokenizeChildren(apiview: a, parent: parent)
        case .compositionTypeElementList:
            tokenizeChildren(apiview: a, parent: parent)
        case .conformanceRequirement:
            tokenizeChildren(apiview: a, parent: parent)
        case .constrainedSugarType:
            tokenizeChildren(apiview: a, parent: parent)
        case .customAttribute:
            tokenizeChildren(apiview: a, parent: parent)
        case .declModifier:
            tokenizeChildren(apiview: a, parent: parent)
        case .designatedTypeList:
            tokenizeChildren(apiview: a, parent: parent)
        case .enumCaseDecl:
            tokenizeChildren(apiview: a, parent: parent)
        case .enumCaseElement:
            // FIXME: Must be commentable
            tokenizeChildren(apiview: a, parent: parent)
        case .enumCaseElementList:
            tokenizeChildren(apiview: a, parent: parent)
        case .floatLiteralExpr:
            tokenizeChildren(apiview: a, parent: parent)
        case .functionCallExpr:
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
            tokenizeChildren(apiview: a, parent: parent)
        case .functionType:
            tokenizeChildren(apiview: a, parent: parent)
        case .genericParameter:
            tokenizeChildren(apiview: a, parent: parent)
        case .genericParameterClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .genericParameterList:
            tokenizeChildren(apiview: a, parent: parent)
        case .genericRequirement:
            tokenizeChildren(apiview: a, parent: parent)
        case .genericRequirementList:
            tokenizeChildren(apiview: a, parent: parent)
        case .genericWhereClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .identifierExpr:
            tokenizeChildren(apiview: a, parent: parent)
        case .identifierPattern:
            let name = IdentifierPatternSyntax(self)!.identifier.withoutTrivia().text
            let lineId = identifier(forName: name, withPrefix: parent?.definitionId)
            a.member(name: name, definitionId: lineId)
        case .inheritedType:
            tokenizeChildren(apiview: a, parent: parent)
        case .inheritedTypeList:
            tokenizeChildren(apiview: a, parent: parent)
        case .initializerClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .initializerDecl:
            DeclarationModel(from: InitializerDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .integerLiteralExpr:
            tokenizeChildren(apiview: a, parent: parent)
        case .memberDeclBlock:
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
            let decl = MemberDeclListItemSyntax(self)!.decl
            let publicModifiers = APIViewModel.publicModifiers
            var showDecl = publicModifiers.contains(decl.modifiers?.accessLevel ?? .unspecified)
            switch decl.kind {
            case .associatedtypeDecl:
                // all associated types are public
                showDecl = true
            case .enumCaseDecl:
                // all enum cases are public
                showDecl = true
            case .functionDecl: fallthrough
            case .initializerDecl: fallthrough
            case .subscriptDecl: fallthrough
            case .typealiasDecl: fallthrough
            case .variableDecl:
                // Public protocols should expose all members even if they have no access level modifier
                if let parentDecl = (parent as? DeclarationModel), parentDecl.isProtocol {
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
        case .memberTypeIdentifier:
            tokenizeChildren(apiview: a, parent: parent)
        case .modifierList:
            tokenizeChildren(apiview: a, parent: parent)
        case .operatorPrecedenceAndTypes:
            tokenizeChildren(apiview: a, parent: parent)
        case .optionalType:
            tokenizeChildren(apiview: a, parent: parent)
        case .packExpansionType:
            tokenizeChildren(apiview: a, parent: parent)
        case .parameterClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .patternBinding:
            tokenizeChildren(apiview: a, parent: parent)
        case .patternBindingList:
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
            tokenizeChildren(apiview: a, parent: parent)
        case .sameTypeRequirement:
            tokenizeChildren(apiview: a, parent: parent)
        case .simpleTypeIdentifier:
            tokenizeChildren(apiview: a, parent: parent)
        case .stringLiteralExpr:
            tokenizeChildren(apiview: a, parent: parent)
        case .stringLiteralSegments:
            tokenizeChildren(apiview: a, parent: parent)
        case .stringSegment:
            tokenizeChildren(apiview: a, parent: parent)
        case .subscriptDecl:
            DeclarationModel(from: SubscriptDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .token:
            let token = TokenSyntax(self)!
            tokenize(token: token, apiview: a)
        case .tupleExprElementList:
            tokenizeChildren(apiview: a, parent: parent)
        case .tupleType:
            tokenizeChildren(apiview: a, parent: parent)
        case .tupleTypeElement:
            tokenizeChildren(apiview: a, parent: parent)
        case .tupleTypeElementList:
            tokenizeChildren(apiview: a, parent: parent)
        case .typealiasDecl:
            DeclarationModel(from: TypealiasDeclSyntax(self)!, parent: parent!).tokenize(apiview: a, parent: parent)
        case .typeAnnotation:
            tokenizeChildren(apiview: a, parent: parent)
        case .typeInheritanceClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .typeInitializerClause:
            tokenizeChildren(apiview: a, parent: parent)
        case .variableDecl:
            tokenizeChildren(apiview: a, parent: parent)
        case .versionTuple:
            tokenizeChildren(apiview: a, parent: parent)
        default:
            SharedLogger.warn("No implmentation for syntax kind: \(self.kind)")
            tokenizeChildren(apiview: a, parent: parent)
        }
    }

    func tokenizeChildren(apiview a: APIViewModel, parent: Linkable?) {
        for child in self.children(viewMode: .sourceAccurate) {
            let childKind = child.kind
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
                // FIXME: May need a parent reference here to resolve references.
                // The use of an associatedtype inside a protocol, for example.
                // This seems to works right now... by accident?
                a.typeReference(name: val)
            }
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

extension DeclSyntax {
    var modifiers: ModifierListSyntax? {
        switch self.kind {
        case .associatedtypeDecl:
            return AssociatedtypeDeclSyntax(self)!.modifiers
        case .enumCaseDecl:
            // enum cases don't have modifiers
            return nil
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
        default:
            SharedLogger.warn("Unhandled syntax type when looking for modifiers: \(self.kind)")
            return nil
        }
    }
}
