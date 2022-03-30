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

class GetterSetterModel: Tokenizable {

    var getMutating: String?
    var getAttributes: AttributesModel
    var setter: String?
    var setMutating: String?
    var setAttributes: AttributesModel?

    init?(from source: GetterSetterKeywordBlock) {
        if source.getter.mutationModifier == .mutating {
            getMutating = "mutating"
        } else {
            getMutating = nil
        }
        getAttributes = AttributesModel(from: source.getter.attributes)
        if let setter = source.setter {
            self.setter = "set"
            if setter.mutationModifier == .mutating {
                setMutating = "mutating"
            } else {
                setMutating = nil
            }
            setAttributes = AttributesModel(from: setter.attributes)
        } else {
            setter = nil
            setMutating = nil
            setAttributes = nil
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.punctuation("{", prefixSpace: true, postfixSpace: true)
        getAttributes.tokenize(apiview: a)
        if let mutating = getMutating {
            a.keyword(mutating, postfixSpace: true)
        }
        a.keyword("get", postfixSpace: true)
        if let setter = setter {
            setAttributes?.tokenize(apiview: a)
            if let mutating = setMutating {
                a.keyword(mutating, postfixSpace: true)
            }
            a.keyword(setter, postfixSpace: true)
        }
        a.punctuation("}")
        a.newline()
    }
}
