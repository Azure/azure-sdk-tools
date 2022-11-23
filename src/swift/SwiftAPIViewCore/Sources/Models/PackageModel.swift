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
        self.process(statements: statements)
    }

    private func process(statements: [CodeBlockItemSyntax.Item]) {
        for statement in statements {
            switch statement {
            case let .decl(decl):
                switch decl.kind {
                case .actorDecl:
                    self.members.append(ActorModel(from: ActorDeclSyntax(decl)!))
                    break
                case .classDecl:
                    self.members.append(ClassModel(from: ClassDeclSyntax(decl)!))
                    break
                case .deinitializerDecl:
                    self.members.append(DeinitializerModel(from: DeinitializerDeclSyntax(decl)!))
                    break
                case .enumDecl:
                    self.members.append(EnumModel(from: EnumDeclSyntax(decl)!))
                    break
                case .extensionDecl:
                    // TODO: Fix how extensions are handled
                    self.members.append(ExtensionModel(from: ExtensionDeclSyntax(decl)!))
                    break
                case .functionDecl:
                    self.members.append(FunctionModel(from: FunctionDeclSyntax(decl)!))
                    break
                case .initializerDecl:
                    self.members.append(InitializerModel(from: InitializerDeclSyntax(decl)!))
                    break
                case .operatorDecl:
                    self.members.append(OperatorModel(from: OperatorDeclSyntax(decl)!))
                    break
                case .precedenceGroupDecl:
                    self.members.append(PrecedenceGroupModel(from: PrecedenceGroupDeclSyntax(decl)!))
                    break
                case .protocolDecl:
                    self.members.append(ProtocolModel(from: ProtocolDeclSyntax(decl)!))
                    break
                case .structDecl:
                    self.members.append(StructModel(from: StructDeclSyntax(decl)!))
                    break
                case .subscriptDecl:
                    self.members.append(SubscriptModel(from: SubscriptDeclSyntax(decl)!))
                    break
                case .typealiasDecl:
                    self.members.append(TypealiasModel(from: TypealiasDeclSyntax(decl)!))
                    break
                case .variableDecl:
                    self.members.append(VariableModel(from: VariableDeclSyntax(decl)!))
                    break
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
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.text("package")
        a.whitespace()
        a.text(name, definitionId: definitionId)
        a.punctuation("{", prefixSpace: true)
        a.newline()
        a.indent {
            for member in members {
                member.tokenize(apiview: a)
            }
        }
        a.punctuation("}")
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
