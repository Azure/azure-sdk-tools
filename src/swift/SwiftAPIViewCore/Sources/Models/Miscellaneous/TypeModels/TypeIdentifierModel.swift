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

struct TypeIdentifierModel: TypeModel {

    struct TypeNameModel: Tokenizable {

        let name: String
        let genericArgument: GenericArgumentModel?

        init(from source: TypeIdentifier.TypeName) {
            name = source.name.textDescription
            genericArgument = GenericArgumentModel(from: source.genericArgumentClause)
        }

        init(name: String) {
            self.name = name
            self.genericArgument = nil
        }

        func tokenize(apiview a: APIViewModel) {
            a.typeReference(name: name)
            genericArgument?.tokenize(apiview: a)
        }
    }

    var optional = OptionalKind.none
    var opaque = OpaqueKind.none
    var attributes: AttributesModel?
    var names: [TypeNameModel]

    init(from source: TypeIdentifier) {
        if let attrs = source.attributes {
            attributes = AttributesModel(from: attrs)
        } else {
            attributes = nil
        }
        names = [TypeNameModel]()
        source.names.forEach { item in
            names.append(TypeNameModel(from: item))
        }
    }

    init(name: String) {
        names = [TypeNameModel(name: name)]
    }

    func tokenize(apiview a: APIViewModel) {
        opaque.tokenize(apiview: a)
        attributes?.tokenize(apiview: a)
        let stopIdx = names.count - 1
        for (idx, name) in names.enumerated() {
            name.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(".")
            }
        }
        optional.tokenize(apiview: a)
    }
}
