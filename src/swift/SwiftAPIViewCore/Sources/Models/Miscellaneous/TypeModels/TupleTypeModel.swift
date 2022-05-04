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

struct TupleTypeModel: TypeModel {

    struct TupleElementModel: Tokenizable {
        let type: TypeModel
        let name: String?
        let attributes: AttributesModel?
        let isInOutParameter: Bool

        init(from source: TupleType.Element) {
            type = source.type.toTokenizable()!
            name = source.name?.textDescription
            attributes = AttributesModel(from: source.attributes)
            isInOutParameter = source.isInOutParameter
        }

        func tokenize(apiview a: APIViewModel) {
            if isInOutParameter {
                a.keyword("inout", postfixSpace: true)
            }
            attributes?.tokenize(apiview: a)
            if let name = name {
                a.member(name: name)
                a.punctuation(":", postfixSpace: true)
            }
            type.tokenize(apiview: a)
        }
    }

    var optional = OptionalKind.none
    var opaque = OpaqueKind.none
    var elements: [TupleElementModel]

    init(from source: TupleType) {
        elements = [TupleElementModel]()
        source.elements.forEach { element in
            elements.append(TupleElementModel(from: element))
        }
    }

    func tokenize(apiview a: APIViewModel) {
        opaque.tokenize(apiview: a)
        a.punctuation("(")
        let stopIdx = elements.count - 1
        for (idx, element) in elements.enumerated() {
            element.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.punctuation(")")
        optional.tokenize(apiview: a)
    }
}
