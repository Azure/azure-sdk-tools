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

struct InitializerItemModel: Tokenizable {

    let name: String
    let typeModel: TypeAnnotationModel?
    let defaultValue: String?

    init(from source: PatternInitializer) {
        name = source.name!
        typeModel = TypeAnnotationModel(from: source.typeModel)
        defaultValue = source.defaultValue
    }

    init(name: String, typeModel: TypeAnnotationModel?, defaultValue: String?) {
        self.name = name
        self.typeModel = typeModel
        self.defaultValue = defaultValue
    }

    func tokenize(apiview a: APIViewModel) {
        a.member(name: name, definitionId: nil)
        if let typeModel = typeModel {
            a.punctuation(":", postfixSpace: true)
            typeModel.tokenize(apiview: a)
        }
        if let defaultValue = defaultValue {
            a.punctuation("=", prefixSpace: true, postfixSpace: true)
            a.literal(defaultValue)
        }
    }
}
