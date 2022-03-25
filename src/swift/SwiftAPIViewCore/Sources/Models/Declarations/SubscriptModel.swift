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

import Foundation
import AST


/// Grammar Summary:
///     subscript-declaration → subscript-head subscript-result generic-where-clause opt code-block
///     subscript-declaration → subscript-head subscript-result generic-where-clause opt getter-setter-block
///     subscript-declaration → subscript-head subscript-result generic-where-clause opt getter-setter-keyword-block
///     subscript-head → attributes opt declaration-modifiers opt subscript generic-parameter-clause opt parameter-clause
///     subscript-result → -> attributes opt type
class SubscriptModel: Tokenizable, Linkable {

    var definitionId: String?
    var accessLevel: String
    var attributes: AttributesModel
    //var modifiers: ModifiersModel
    var genericParamClause: GenericParameterModel?
    var genericWhereClause: GenericWhereModel?
    //var resultType: ResultTypeModel
    var parameterList: [Tokenizable]
    //var getterSetterKeywordBlock: GetterSetterModel

    init(from decl: SubscriptDeclaration) {
        self.definitionId = ""
        self.accessLevel = ""
        self.attributes = AttributesModel(from: decl.attributes)
        self.genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        self.genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        self.parameterList = [Tokenizable]()
        decl.parameterList.forEach { param in
            // FIXME: Finish!
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        //guard publicModifiers.contains(accessLevel) else { return false }
        t.append(contentsOf: attributes.tokenize())
        //handle(modifiers: modifiers)
        t.keyword("subscript", definitionId: definitionId)
        t.append(contentsOf: genericParamClause?.tokenize() ?? [])
        t.punctuation("(")
        parameterList.forEach { param in
            t.append(contentsOf: param.tokenize())
        }
        t.punctuation(")")
        t.whitespace()
        //handle(result: resultType, defId: defId)
        t.append(contentsOf: genericWhereClause?.tokenize() ?? [])
        //handle(clause: getterSetterKeywordBlock)
        t.newLine()
        return t
    }
}
