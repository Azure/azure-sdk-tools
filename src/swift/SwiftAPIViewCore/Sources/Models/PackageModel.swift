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

import AST
import Foundation


class PackageModel: Tokenizable, Navigable {

    let name: String
    var children: [Tokenizable]

    init(name: String, statements: [Statement]) {
        self.name = name
        self.children = [Tokenizable]()
        self.process(statements: statements)
    }

    private func process(statements: [Statement]) {
        for statement in statements {
            switch statement {
            case let decl as Declaration:
                self.process(declaration: decl)
            default:
                SharedLogger.fail("Unsupported statement type: \(statement)")
            }
        }
    }

    private func process(declaration decl: Declaration) {
        var child: Tokenizable
        switch decl {
        case let decl as ClassDeclaration:
            child = ClassModel(from: decl)
        case let decl as ConstantDeclaration:
            child = ConstantModel(from: decl)
        case let decl as EnumDeclaration:
            child = EnumModel(from: decl)
        case let decl as ExtensionDeclaration:
            child = ExtensionModel(from: decl)
        case let decl as FunctionDeclaration:
            child = FunctionModel(from: decl)
        case let decl as InitializerDeclaration:
            child = InitializerModel(from: decl)
        case let decl as ProtocolDeclaration:
            child = ProtocolModel(from: decl)
        case let decl as StructDeclaration:
            child = StructModel(from: decl)
        case let decl as TypealiasDeclaration:
            child = TypealiasModel(from: decl)
        case let decl as VariableDeclaration:
            child = VariableModel(from: decl)
        case _ as ImportDeclaration:
            // Imports are no-op
            return
        case _ as DeinitializerDeclaration:
            // Deinitializers are never public
            return
        case let decl as SubscriptDeclaration:
            child = SubscriptModel(from: decl)
        case let decl as PrecedenceGroupDeclaration:
            // precedence groups are always public
            child = PrecedenceGroupModel(from: decl)
        case let decl as OperatorDeclaration:
            // operators are always public
            child = OperatorModel(from: decl)
        default:
            SharedLogger.fail("Unsupported declaration: \(decl)")
        }
        self.children.append(child)
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        t.text("package")
        t.whitespace()
        // FIXME: This!
        let defId = ""
        t.text(name, definitionId: defId)
        t.whitespace()
        t.punctuation("{")
        t.newLine()
        for child in children {
            t.append(contentsOf: child.tokenize())
        }
        t.punctuation("}")
        t.newLine()
        return t
    }

    func navigationTokenize() -> [NavigationToken] {
        var t = [NavigationToken]()
        for child in children {
            guard let child = child as? Navigable else { continue }
            t.append(contentsOf: child.navigationTokenize())
        }
        return t
    }
}
