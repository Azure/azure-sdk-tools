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
    var members: [DeclarationModel]
    var extensions: [ExtensionModel]

    init(name: String, statements: [CodeBlockItemSyntax.Item]) {
        self.name = name
        self.definitionId = name
        self.parent = nil
        self.members = [DeclarationModel]()
        self.extensions = []
        process(statements: statements)
        resolveDuplicateMembers()
        processExtensions()
    }

    func process(statements: [CodeBlockItemSyntax.Item]) {
        for statement in statements {
            switch statement {
            case let .decl(decl):
                switch decl.kind {
                case .actorDecl:
                    appendIfVisible(DeclarationModel(from: ActorDeclSyntax(decl)!, parent: self))
                case .classDecl:
                    appendIfVisible(DeclarationModel(from: ClassDeclSyntax(decl)!, parent: self))
                case .deinitializerDecl:
                    // deinitializers cannot be called by users, so it makes no sense
                    // to expose them in APIView
                    break
                case .enumDecl:
                    appendIfVisible(DeclarationModel(from: EnumDeclSyntax(decl)!, parent: self))
                case .functionDecl:
                    appendIfVisible(DeclarationModel(from: FunctionDeclSyntax(decl)!, parent: self))
                case .importDecl:
                    // purposely ignore import declarations
                    break
                case .operatorDecl:
                    let model = DeclarationModel(from: OperatorDeclSyntax(decl)!, parent: self)
                    // operators are global and must always be displayed
                    model.accessLevel = .public
                    appendIfVisible(model)
                case .precedenceGroupDecl:
                    let model = DeclarationModel(from: PrecedenceGroupDeclSyntax(decl)!, parent: self)
                    // precedence groups are public and must always be displayed
                    model.accessLevel = .public
                    appendIfVisible(model)
                case .protocolDecl:
                    appendIfVisible(DeclarationModel(from: ProtocolDeclSyntax(decl)!, parent: self))
                case .structDecl:
                    appendIfVisible(DeclarationModel(from: StructDeclSyntax(decl)!, parent: self))
                case .typealiasDecl:
                    appendIfVisible(DeclarationModel(from: TypealiasDeclSyntax(decl)!, parent: self))
                case .extensionDecl:
                    extensions.append(ExtensionModel(from: ExtensionDeclSyntax(decl)!))
                case .initializerDecl:
                    appendIfVisible(DeclarationModel(from: InitializerDeclSyntax(decl)!, parent: self))
                case .subscriptDecl:
                    appendIfVisible(DeclarationModel(from: SubscriptDeclSyntax(decl)!, parent: self))
                case .variableDecl:
                    appendIfVisible(DeclarationModel(from: VariableDeclSyntax(decl)!, parent: self))
                default:
                    // Create an generic declaration model of unknown type
                    appendIfVisible(DeclarationModel(from: decl, parent: self))
                }
            default:
                SharedLogger.warn("Unexpectedly encountered a non-declaration statement: \(statement.kind). APIView may not display correctly.")
            }
        }
    }

    func tokenize(apiview a: APIViewModel, parent: Linkable?) {
        a.text("package")
        a.whitespace()
        a.text(name, definitionId: definitionId)
        a.punctuation("{", spacing: SwiftSyntax.TokenKind.leftBrace.spacing)
        a.newline()
        a.indent {
            for member in members {
                member.tokenize(apiview: a, parent: self)
                a.blankLines(set: 1)
            }
            // render any orphaned extensions
            if !extensions.isEmpty {
                a.comment("Non-package extensions")
                a.newline()
                let endIdx = extensions.count - 1
                for (idx, ext) in extensions.enumerated() {
                    ext.tokenize(apiview: a, parent: nil)
                    if idx != endIdx {
                        a.blankLines(set: 1)
                    }
                }
            }
        }
        a.punctuation("}", spacing: SwiftSyntax.TokenKind.rightBrace.spacing)
        a.newline()
        resolveTypeReferences(apiview: a)
    }

    /// Move extensions into the model representations for declared package types
    func processExtensions() {
        var otherExtensions = [ExtensionModel]()
        for ext in extensions {
            let extendedType = ext.extendedType
            if let parentMatch = findDeclaration(for: extendedType) {
                parentMatch.extensions.append(ext)
            } else {
                otherExtensions.append(ext)
            }
        }
        self.extensions = otherExtensions
        // process orphaned extensions
        for ext in extensions {
            ext.processMembers(withParent: nil)
        }
        extensions = extensions.resolveDuplicates()
        // process all extensions associated with members
        for member in members {
            for ext in member.extensions {
                ext.processMembers(withParent: ext.parent)
            }
            member.extensions = member.extensions.resolveDuplicates()
        }
    }

    /// Ensure members are not duplicated (may happen with framework files)
    func resolveDuplicateMembers() {
        var uniqueMembers = [String: DeclarationModel]()
        for member in members {
            if let match = uniqueMembers[member.definitionId!] {
                if match != member {
                    SharedLogger.warn("Member \(member.name) differs for what already exists and will be ignored. Swift APIView may not display correctly.")
                }
            } else {
                uniqueMembers[member.definitionId!] = member
            }
        }
        members = Array(uniqueMembers.values)
        members = members.sorted(by: { $0.definitionId! < $1.definitionId! })
    }

    /// attempt to resolve type references that are declared after they are used
    func resolveTypeReferences(apiview a: APIViewModel) {
        for (idx, token) in a.tokens.enumerated() {
            guard token.navigateToId == APIViewModel.unresolved else { continue }
            a.tokens[idx].navigateToId = a.definitionId(for: token.value!, withParent: nil)
            assert (a.tokens[idx].navigateToId != APIViewModel.unresolved)
        }
    }

    func appendIfVisible(_ decl: DeclarationModel) {
        if decl.shouldShow() {
            members.append(decl)
        }
    }

    func findDeclaration(for name: String) -> DeclarationModel? {
        let result = members.filter { decl in
            decl.name == name
        }
        if result.count > 1 {
            SharedLogger.warn("Unexpectedly found \(result.count) matches for type \(name).")
        }
        return result.first
    }

    func navigationTokenize(apiview a: APIViewModel, parent: Linkable?) {
        let packageToken = NavigationToken(name: name, navigationId: name, typeKind: .assembly, members: members)
        a.add(token: packageToken)
        if !extensions.isEmpty {
            let extensionsToken = NavigationToken(name: "Other Extensions", navigationId: "", typeKind: .assembly, extensions: extensions)
            a.add(token: extensionsToken)
        }
    }
}
