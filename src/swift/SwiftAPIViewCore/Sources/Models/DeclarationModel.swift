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



class DeclarationModel: Tokenizable, Linkable, Equatable {

    var accessLevel: AccessLevel
    var name: String
    var definitionId: String?
    var lineId: String?
    var parent: Linkable?
    let childNodes: SyntaxChildren
    var extensions: [ExtensionModel]
    let kind: Kind

    enum Kind {
        case `class`
        case `enum`
        case method
        case package
        case `protocol`
        case `struct`
        case unknown
        case variable

        var navigationSymbol: NavigationTypeKind {
            switch self {
            case .class: return .class
            case .struct: return .struct
            case .enum: return .enum
            // nothing for methods?
            case .method: return .interface
            case .package: return .assembly
            case .protocol: return .interface
            case .unknown: return .unknown
            // nothing for variables really
            case .variable: return .struct
            }
        }
    }

    init(name: String, decl: SyntaxProtocol, defId: String, parent: Linkable?, kind: Kind) {
        self.name = name
        self.parent = parent
        self.accessLevel = (decl as? hasModifiers)?.modifiers.accessLevel ?? .unspecified
        self.childNodes = decl.children(viewMode: .sourceAccurate)
        self.definitionId = defId
        self.lineId = nil
        self.kind = kind
        self.extensions = []
    }

    /// Initialize from function declaration
    convenience init(from decl: FunctionDeclSyntax, parent: Linkable?) {
        let name = decl.identifier.withoutTrivia().text
        let defId = identifier(forName: name, withSignature: decl.signature, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .method)
    }

    /// Initialize from protocol declaration
    convenience init(from decl: ProtocolDeclSyntax, parent: Linkable?) {
        let name = decl.identifier.withoutTrivia().text
        let defId = identifier(forName: name, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .protocol)
    }

    /// Initialize from initializer declaration
    convenience init(from decl: InitializerDeclSyntax, parent: Linkable?) {
        let name = "init"
        let defId = identifier(forName: name, withSignature: decl.signature, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .method)
    }

    /// Initialize from subscript declaration
    convenience init(from decl: SubscriptDeclSyntax, parent: Linkable?) {
        let name = "subscript"
        let defId = identifier(for: decl, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .method)
    }

    /// Initialize from class type declaration
    convenience init(from decl: ClassDeclSyntax, parent: Linkable?) {
        let name = decl.identifier.withoutTrivia().text
        let defId = identifier(forName: name, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .class)
    }

    /// Initialize from associated type declaration
    convenience init(from decl: AssociatedtypeDeclSyntax, parent: Linkable?) {
        let name = decl.identifier.withoutTrivia().text
        let defId = identifier(forName: name, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .struct)
    }

    /// Used for most declaration types that have members
    convenience init(from decl: SyntaxProtocol, parent: Linkable?) {
        let name = (decl as? hasIdentifier)!.identifier.withoutTrivia().text

        let defId = identifier(forName: name, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .struct)
    }

    /// Used for variable declarations
    convenience init(from decl: VariableDeclSyntax, parent: Linkable?) {
        let name = decl.bindings.names.joined(separator: ",")
        let defId = identifier(forName: name, withPrefix: parent?.definitionId)
        self.init(name: name, decl: decl, defId: defId, parent: parent, kind: .variable)
    }

    /// Used when the declaration type is unknown
    init(from decl: DeclSyntax, parent: Linkable?) {
        SharedLogger.warn("Unexpected declaration type: \(decl.kind). This may not appear correctly in APIView.")
        self.parent = parent
        let name = (decl as? hasIdentifier)?.identifier.withoutTrivia().text ?? ""
        definitionId = identifier(forName: name, withPrefix: parent?.definitionId)
        lineId = nil
        self.name = name
        self.childNodes = decl.children(viewMode: .sourceAccurate)
        self.accessLevel = (decl as? hasModifiers)?.modifiers.accessLevel ?? .unspecified
        self.kind = .unknown
        self.extensions = []
    }

    func tokenize(apiview a: CodeModel, parent: Linkable?) {
        for (idx, child) in childNodes.enumerated() {
            switch child.kind {
            case .attributeList:
                // Ensure declarations that have an attributeList have a blank line,
                // unless it is the first child
                a.blankLines(set: a.currentParent?.children.count ?? 0 == 0 ? 0 : 1)
                // attributes on declarations should have newlines
                let obj = AttributeListSyntax(child)!
                let children = obj.children(viewMode: .sourceAccurate)
                for attr in children {
                    let attrText = attr.withoutTrivia().description.filter { !$0.isWhitespace }
                    a.lineMarker("\(definitionId!).\(attrText)")
                    a.currentLine.relatedToLine = definitionId
                    attr.tokenize(apiview: a, parent: parent)
                    a.blankLines(set: 0)
                }
            case .precedenceGroupAttributeList:
                let obj = PrecedenceGroupAttributeListSyntax(child)!
                a.indent {
                    for item in obj.children(viewMode: .sourceAccurate) {
                        item.tokenize(apiview: a, parent: nil)
                    }
                    a.blankLines(set: 0)
                }
            case .token:
                if child.withoutTrivia().description == self.name {
                    // render as the correct APIView token type
                    switch self.kind {
                    case .class:
                        a.typeDeclaration(name: name, typeId: definitionId)
                    case .enum:
                        a.typeDeclaration(name: name, typeId: definitionId)
                    case .method:
                        a.lineMarker(definitionId)
                        a.member(name: name)
                    case .package:
                        a.typeDeclaration(name: name, typeId: definitionId)
                    case .protocol:
                        a.typeDeclaration(name: name, typeId: definitionId)
                    case .struct:
                        a.typeDeclaration(name: name, typeId: definitionId)
                    case .unknown:
                        a.typeDeclaration(name: name, typeId: definitionId)
                    case .variable:
                        a.lineMarker(definitionId)
                        a.member(name: name)
                    }
                } else {
                    child.tokenize(apiview: a, parent: nil)
                }
            default:
                // call default implementation in SyntaxProtocol+Extensions
                child.tokenize(apiview: a, parent: self)
            }
        }
        if !extensions.isEmpty {
            extensions.tokenize(apiview: a, parent: self)
        }
    }

    func shouldShow() -> Bool {
        let publicModifiers = CodeModel.publicModifiers
        guard let parentDecl = (parent as? DeclarationModel) else {
            return publicModifiers.contains(self.accessLevel)
        }
        if parentDecl.kind == .protocol {
            let parentAccess = parentDecl.accessLevel
            return publicModifiers.contains(self.accessLevel) || publicModifiers.contains(parentAccess)
        } else {
            return publicModifiers.contains(self.accessLevel)
        }
    }

    static func == (lhs: DeclarationModel, rhs: DeclarationModel) -> Bool {
        if lhs.name == rhs.name &&
            lhs.definitionId == rhs.definitionId &&
            lhs.kind == rhs.kind &&
            lhs.accessLevel == rhs.accessLevel &&
            lhs.lineId == rhs.lineId &&
            lhs.extensions.count == rhs.extensions.count &&
            lhs.childNodes.count == rhs.childNodes.count {
            return true
        } else {
            return false
        }
    }
}
