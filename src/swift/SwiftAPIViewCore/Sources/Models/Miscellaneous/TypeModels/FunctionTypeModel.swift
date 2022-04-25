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

struct FunctionTypeModel: TypeModel {

    struct ArgumentModel: Tokenizable {
        let externalName: String?
        let localName: String?
        let typeModel: TypeModel
        let attributes: AttributesModel?
        let isInOut: Bool
        let isVariadic: Bool

        init(from source: FunctionType.Argument) {
            externalName = source.externalName?.textDescription
            localName = source.localName?.textDescription
            typeModel = source.type.toTokenizable()!
            attributes = AttributesModel(from: source.attributes)
            isInOut = source.isInOutParameter
            isVariadic = source.isVariadic
        }

        func tokenize(apiview a: APIViewModel) {
            if let name = externalName ?? localName {
                a.member(name: name)
                a.punctuation(":", postfixSpace: true)
            }
            attributes?.tokenize(apiview: a)
            if isInOut {
                a.keyword("inout", prefixSpace: true, postfixSpace: true)
            }
            typeModel.tokenize(apiview: a)
            if isVariadic {
                a.text("...")
            }
        }
    }

    var optional = OptionalKind.none
    var opaque = OpaqueKind.none
    var attributes: AttributesModel?
    var arguments: [ArgumentModel]
    var returnType: TypeModel
    var `throws`: ThrowsKind

    init(from source: FunctionType) {
        attributes = AttributesModel(from: source.attributes)
        returnType = source.returnType.toTokenizable()!
        self.throws = source.throwsKind
        arguments = [ArgumentModel]()
        source.arguments.forEach { arg in
            arguments.append(ArgumentModel(from: arg))
        }
    }

    func tokenize(apiview a: APIViewModel) {
        opaque.tokenize(apiview: a)
        a.punctuation("(")
        let stopIdx = arguments.count - 1
        for (idx, arg) in arguments.enumerated() {
            arg.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.punctuation(")", postfixSpace: true)
        self.throws.tokenize(apiview: a)
        a.punctuation("->", prefixSpace: true, postfixSpace: true)
        returnType.tokenize(apiview: a)
        optional.tokenize(apiview: a)
    }
}
