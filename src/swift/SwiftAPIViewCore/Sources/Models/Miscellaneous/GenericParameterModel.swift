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


class GenericParameterModel: Tokenizable {

    /// The list of type or protocol conformances
    var typeList: [Tokenizable]

    init?(from clause: GenericParameterClause?) {
        guard let clause = clause else { return nil }
        self.typeList = [Tokenizable]()
        clause.parameterList.forEach { param in
            switch param {
            case let .identifier(type1):
                typeList.append(TypeIdentifierModel(name: type1.textDescription))
            case let .protocolConformance(type1, protocol2):
                typeList.append(GenericRequirementModel(key: type1, value: protocol2, mode: .conformance))
            case let .typeConformance(type1, type2):
                typeList.append(GenericRequirementModel(key: type1, value: type2, mode: .conformance))
            }
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.punctuation("<")
        let stopIdx = typeList.count - 1
        for (idx, param) in typeList.enumerated() {
            param.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.punctuation(">")
    }
}
