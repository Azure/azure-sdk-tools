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

class GenericRequirementModel: Tokenizable {

    enum Mode {
        case conformance
        case equality

        var punctuation: String {
            switch self {
            case .conformance:
                return ":"
            case .equality:
                return "=="
            }
        }
    }

    var key: TypeModel
    var value: TypeModel
    var mode: Mode

    init(key: Type, value: Type, mode: Mode) {
        self.key = key.toTokenizable()!
        self.value = value.toTokenizable()!
        self.mode = mode
    }

    init(key: Identifier, value: Type, mode: Mode) {
        self.key = TypeIdentifierModel(name: key.textDescription)
        self.value = value.toTokenizable()!
        self.mode = mode
    }

    init(key: Identifier, value: Identifier, mode: Mode) {
        self.key = TypeIdentifierModel(name: key.textDescription)
        self.value = TypeIdentifierModel(name: value.textDescription)
        self.mode = mode
    }

    func tokenize(apiview a: APIViewModel) {
        key.tokenize(apiview: a)
        a.punctuation(mode.punctuation, prefixSpace: mode == .equality, postfixSpace: true)
        value.tokenize(apiview: a)
    }
}
