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
    var members: [Tokenizable]

    init(name: String, statements: [CodeBlockItemSyntax.Item]) {
        self.name = name
        self.definitionId = name
        self.parent = nil
        self.members = [Tokenizable]()
        process(statements: statements)
    }

    func process(statements: [CodeBlockItemSyntax.Item]) {
        for statement in statements {
            switch statement {
            case let .decl(decl):
                switch decl.kind {
                case .actorDecl:
                    members.append(DeclarationModel(from: ActorDeclSyntax(decl)!, parent: self))
                case .classDecl:
                    members.append(DeclarationModel(from: ClassDeclSyntax(decl)!, parent: self))
                case .deinitializerDecl:
                    // deinitializers cannot be called by users, so it makes no sense
                    // to expose them in APIView
                    break
                case .enumDecl:
                    members.append(DeclarationModel(from: EnumDeclSyntax(decl)!, parent: self))
                case .functionDecl:
                    members.append(DeclarationModel(from: FunctionDeclSyntax(decl)!, parent: self))
                case .importDecl:
                    // purposely ignore import declarations
                    break
                case .operatorDecl:
                    let model = DeclarationModel(from: OperatorDeclSyntax(decl)!, parent: self)
                    // operators are global and must always be displayed
                    model.accessLevel = .public
                    members.append(model)
                case .precedenceGroupDecl:
                    let model = DeclarationModel(from: PrecedenceGroupDeclSyntax(decl)!, parent: self)
                    // precedence groups are public and must always be displayed
                    model.accessLevel = .public
                    members.append(model)
                case .protocolDecl:
                    members.append(DeclarationModel(from: ProtocolDeclSyntax(decl)!, parent: self))
                case .structDecl:
                    members.append(DeclarationModel(from: StructDeclSyntax(decl)!, parent: self))
                case .typealiasDecl:
                    members.append(DeclarationModel(from: TypealiasDeclSyntax(decl)!, parent: self))
                case .extensionDecl:
                    // TODO: implement this
                    break
                case .initializerDecl:
                    // TODO: implement this
                    break
                case .subscriptDecl:
                    // TODO: implement this
                    break
                case .variableDecl:
                    // TODO: implement this
                    break
                default:
                    // Create an generic declaration model of unknown type
                    members.append(DeclarationModel(from: decl, parent: self))
                }
            default:
                SharedLogger.fail("Unexpectedly encountered a non-declaration: \(statement.kind)")
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
        }
        a.punctuation("}", spacing: SwiftSyntax.TokenKind.rightBrace.spacing)
        a.newline()
    }

    func navigationTokenize(apiview a: APIViewModel) {
        for member in members {
            guard let member = member as? Linkable else { continue }
            member.navigationTokenize(apiview: a)
        }
        // sort the root navigation elements by name
        a.navigation.sort { $0.name < $1.name }
    }
}
