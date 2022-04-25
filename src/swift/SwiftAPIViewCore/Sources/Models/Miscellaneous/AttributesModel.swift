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
        // display atributes with no arguments as inline
        isInline = argumentClause == nil
    }

    func tokenize(apiview a: APIViewModel) {
        a.keyword(name)
        if let argument = argumentClause {
            a.text(argument)
        }
        isInline ? a.whitespace() : a.newline()
    }
}

class AttributesModel: Tokenizable {

    var attributes: [AttributeModel]

    init(from clause: Attributes) {
        self.attributes = [AttributeModel]()
        clause.forEach { attr in
            self.attributes.append(AttributeModel(from: attr))
        }
    }

    func tokenize(apiview a: APIViewModel) {
        attributes.forEach { attribute in
            attribute.tokenize(apiview: a)
        }
    }
}
