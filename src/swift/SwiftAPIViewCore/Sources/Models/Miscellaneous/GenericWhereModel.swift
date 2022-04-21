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

class GenericWhereModel: Tokenizable {

    var requirementsList: [Tokenizable]

    init?(from clause: GenericWhereClause?) {
        guard let clause = clause else { return nil }
        self.requirementsList = [Tokenizable]()
        clause.requirementList.forEach { item in
            switch item {
            case let .protocolConformance(type1, protocol2):
                requirementsList.append(GenericRequirementModel(key: type1, value: protocol2, mode: .conformance))
            case let .typeConformance(type1, type2):
                requirementsList.append(GenericRequirementModel(key: type1, value: type2, mode: .conformance))
            case let .sameType(type1, type2):
                requirementsList.append(GenericRequirementModel(key: type1, value: type2, mode: .equality))
            }
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.keyword("where", prefixSpace: true, postfixSpace: true)
        let stopIdx = requirementsList.count - 1
        for (idx, item) in requirementsList.enumerated() {
            item.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.whitespace()
    }
}
