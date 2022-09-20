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

class TypeInheritanceModel: Tokenizable {

    var isClassRequirement: Bool
    var typeList: [TypeModel]

    init?(from clause: TypeInheritanceClause?) {
        guard let clause = clause else { return nil }
        self.isClassRequirement = clause.classRequirement
        self.typeList = [TypeModel]()
        clause.typeInheritanceList.forEach { item in
            typeList.append(TypeIdentifierModel(from: item))
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.punctuation(":", postfixSpace: true)
        if isClassRequirement {
            a.keyword("class")
            if typeList.count > 0 { a.punctuation(",", postfixSpace: true) }
        }
        let stopIdx = typeList.count - 1
        for (idx, item) in typeList.enumerated() {
            item.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.whitespace()
    }
}
