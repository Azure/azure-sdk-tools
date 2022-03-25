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

struct AttributeModel: Tokenizable {

    /// Name of the attribute
    var name: String

    /// Optional argument clause
    var argumentClause: String?

    /// Whether the attribute is rendered inline or on a separate line
    var isInline: Bool

    init(from clause: Attribute) {
        name = "@\(clause.name)"
        argumentClause = clause.argumentClause?.textDescription
        isInline = false
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        t.keyword(name, definitionId: nil)
        if let argument = argumentClause {
            t.text(argument)
        }
        isInline ? t.whitespace() : t.newLine()
        return t
    }
}

class AttributesModel: Tokenizable {

    var attributes: [AttributeModel]

    init(from clause: Attributes) {
        self.attributes = [AttributeModel]()
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        attributes.forEach { attribute in
            t.append(contentsOf: attribute.tokenize())
        }
        return t
    }
}
