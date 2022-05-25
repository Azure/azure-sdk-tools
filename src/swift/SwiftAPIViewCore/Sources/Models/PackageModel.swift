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


class PackageModel: Tokenizable, Linkable {

    let name: String
    var definitionId: String?
    var parent: Linkable?
    var members: [Tokenizable]

    init(name: String, statements: [Statement]) {
        self.name = name
        self.definitionId = name
        self.parent = nil
        self.members = [Tokenizable]()
        self.process(statements: statements)
    }

    private func process(statements: [Statement]) {
        for statement in statements {
            switch statement {
            case let decl as Declaration:
                if let model = decl.toTokenizable(withParent: self) {
                    if let model = model as? ExtensionModel {
                        // TODO: Place the extension in the appropriate location
                        self.members.append(model)
                    } else {
                        self.members.append(model)
                    }
                }
            case _ as CompilerControlStatement:
                break
            default:
                SharedLogger.fail("Unsupported statement type: \(statement)")
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
