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
import AST


/// Grammar Summary:
///     precedence-group-declaration → precedencegroup precedence-group-name { precedence-group-attributes opt }
///     precedence-group-attributes → precedence-group-attribute precedence-group-attributes opt
///     precedence-group-attribute → precedence-group-relation
///     precedence-group-attribute → precedence-group-assignment
///     precedence-group-attribute → precedence-group-associativity
///     precedence-group-relation → higherThan : precedence-group-names
///     precedence-group-relation → lowerThan : precedence-group-names
///     precedence-group-assignment → assignment : boolean-literal
///     precedence-group-associativity → associativity : left
///     precedence-group-associativity → associativity : right
///     precedence-group-associativity → associativity : none
///     precedence-group-names → precedence-group-name | precedence-group-name , precedence-group-names
///     precedence-group-name → identifier
class PrecedenceGroupModel: Tokenizable, Navigable, Linkable {

    var definitionId: String?
    var name: String
    var attributes: [Tokenizable]

    init(from decl: PrecedenceGroupDeclaration) {
        self.name = ""
        //self.definitionId = = self.defId(forName: name, withPrefix: defIdPrefix)
        decl.attributes.forEach { item in
//                switch attr {
//                case let .assignment(val):
//                    let defId = self.defId(forName: "assignment", withPrefix: defId)
//                    keyword(value: "assignment", definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    keyword(value: String(val))
//                case .associativityLeft:
//                    let defId = self.defId(forName: "associativity", withPrefix: defId)
//                    keyword(value: "associativity", definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    keyword(value: "left")
//                case .associativityNone:
//                    let defId = self.defId(forName: "associativity", withPrefix: defId)
//                    keyword(value: "associativity", definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    keyword(value: "none")
//                case .associativityRight:
//                    let defId = self.defId(forName: "associativity", withPrefix: defId)
//                    keyword(value: "associativity", definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    keyword(value: "right")
//                case let .higherThan(val):
//                    let defId = self.defId(forName: "higherThan", withPrefix: defId)
//                    keyword(value: "higherThan", definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    typeReference(name: val.map { $0.textDescription }.joined(separator: "."))
//                case  let .lowerThan(val):
//                    let defId = self.defId(forName: "lowerThan", withPrefix: defId)
//                    keyword(value: "lowerThan", definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    typeReference(name: val.map { $0.textDescription }.joined(separator: "."))
//                }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        t.keyword("precedencegroup")
        t.whitespace()
        t.typeDeclaration(name: name, definitionId: definitionId)
        t.whitespace()
        t.punctuation("{")
        t.newLine()
        attributes.forEach { item in
            t.append(contentsOf: item.tokenize())
            t.newLine()
        }
        t.punctuation("}")
        t.newLine()
        return t
    }

    func navigationTokenize() -> [NavigationToken] {
        var t = [NavigationToken]()
        //        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .unknown)
        return t
    }
}
