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


class ExtensionModel {
    var accessLevel: AccessLevel
    var extendedType: String
    var definitionId: String
    var members: [DeclarationModel]
    let childNodes: SyntaxChildren

    /// Initialize from initializer declaration
    init(from decl: ExtensionDeclSyntax) {
        self.accessLevel = decl.modifiers.accessLevel
        self.childNodes = decl.children(viewMode: .sourceAccurate)
        let extType = decl.extendedType
        switch extType.kind {
        case .simpleTypeIdentifier:
            self.extendedType = SimpleTypeIdentifierSyntax(extType)!.name.withoutTrivia().description
        default:
            SharedLogger.warn("Unhandled extended type kind: \(extType.kind). APIView may not display correctly")
            self.extendedType = "_UNKNOWN_"
        }
        self.members = [DeclarationModel]()
        self.definitionId = ""
        self.definitionId = identifier(forType: decl.extendedType, inheritClause: decl.inheritanceClause, whereClause: decl.genericWhereClause)
        process(members: decl.members.members)
    }

    func identifier(forType type: TypeSyntax, inheritClause: TypeInheritanceClauseSyntax?, whereClause: GenericWhereClauseSyntax?) -> String {
        var defId = ""
        let a = APIViewModel(name: "", packageName: "", versionString: "", statements: [])
        a.tokens = []
        type.tokenize(apiview: a, parent: nil)
        inheritClause?.tokenize(apiview: a, parent: nil)
        whereClause?.tokenize(apiview: a, parent: nil)
        let tokens = a.tokens
        tokens.forEach { token in
            if let val = token.value {
                defId.append(val)
            }
        }
        defId = defId.replacingOccurrences(of: " ", with: "_")
        return defId
    }

    func process(members: MemberDeclListSyntax) {
        for member in members {
            let decl = member.decl
            let declKind = decl.kind
            switch decl.kind {
                case .actorDecl:
                    appendIfVisible(DeclarationModel(from: ActorDeclSyntax(decl)!, parent: nil))
                case .classDecl:
                    appendIfVisible(DeclarationModel(from: ClassDeclSyntax(decl)!, parent: nil))
                case .deinitializerDecl:
                    // deinitializers cannot be called by users, so it makes no sense
                    // to expose them in APIView
                    break
                case .enumDecl:
                    appendIfVisible(DeclarationModel(from: EnumDeclSyntax(decl)!, parent: nil))
                case .functionDecl:
                    appendIfVisible(DeclarationModel(from: FunctionDeclSyntax(decl)!, parent: nil))
                case .importDecl:
                    // purposely ignore import declarations
                    break
                case .operatorDecl:
                    let model = DeclarationModel(from: OperatorDeclSyntax(decl)!, parent: nil)
                    // operators are global and must always be displayed
                    model.accessLevel = .public
                    appendIfVisible(model)
                case .precedenceGroupDecl:
                    let model = DeclarationModel(from: PrecedenceGroupDeclSyntax(decl)!, parent: nil)
                    // precedence groups are public and must always be displayed
                    model.accessLevel = .public
                    appendIfVisible(model)
                case .protocolDecl:
                    appendIfVisible(DeclarationModel(from: ProtocolDeclSyntax(decl)!, parent:  nil))
                case .structDecl:
                    appendIfVisible(DeclarationModel(from: StructDeclSyntax(decl)!, parent: nil))
                case .typealiasDecl:
                    appendIfVisible(DeclarationModel(from: TypealiasDeclSyntax(decl)!, parent: nil))
                case .extensionDecl:
                    SharedLogger.warn("Extensions containing extensions is not supported. Contact the Swift APIView team.")
                case .initializerDecl:
                    appendIfVisible(DeclarationModel(from: InitializerDeclSyntax(decl)!, parent: nil))
                case .subscriptDecl:
                    appendIfVisible(DeclarationModel(from: SubscriptDeclSyntax(decl)!, parent: nil))
                case .variableDecl:
                    appendIfVisible(DeclarationModel(from: VariableDeclSyntax(decl)!, parent: nil))
                default:
                    // Create an generic declaration model of unknown type
                    appendIfVisible(DeclarationModel(from: decl, parent: nil))
                }
        }
    }

    func appendIfVisible(_ decl: DeclarationModel) {
        if decl.shouldShow(inExtension: self) {
            self.members.append(decl)
        }
    }
}
