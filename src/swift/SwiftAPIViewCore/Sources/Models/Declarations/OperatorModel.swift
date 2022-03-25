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
///     operator-declaration → prefix-operator-declaration | postfix-operator-declaration | infix-operator-declaration
///     prefix-operator-declaration → prefix operator operator
///     postfix-operator-declaration → postfix operator operator
///     infix-operator-declaration → infix operator operator infix-operator-group opt
///     infix-operator-group → : precedence-group-name
class OperatorModel: Tokenizable, Navigable, Linkable {

    var definitionId: String?
    var kword: String
    var name: String?
    var opName: String

    init(from decl: OperatorDeclaration) {
        switch decl.kind {
        case let .infix(op, ident):
            self.kword = "infix"
            self.opName = op
            self.name = ident?.textDescription
        case let .prefix(op):
            self.kword = "prefix"
            self.opName = op
        case let .postfix(op):
            self.kword = "postfix"
            self.opName = op
        }
        // FIXME: This!
        //self.definitionId = defId(forName: opName, withPrefix: defIdPrefix)
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        t.keyword(kword)
        t.whitespace()
        t.keyword("operator")
        t.whitespace()
        t.text(opName, definitionId: definitionId)
        if let name = name {
            t.punctuation(":")
            t.whitespace()
            t.typeReference(name: name)
        }
        t.newLine()
        return t
    }

    func navigationTokenize() -> [NavigationToken] {
        var t = [NavigationToken]()
        //        let navItem = NavigationItem(name: decl.operator, prefix: prefix, typeKind: .unknown)
        return t
    }
}
