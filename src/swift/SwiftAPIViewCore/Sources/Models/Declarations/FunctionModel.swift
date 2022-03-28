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
///     function-declaration → function-head function-name generic-parameter-clause opt function-signature generic-where-clause opt function-body opt
///     function-head → attributes opt declaration-modifiers opt func
///     function-name → identifier | operator
///     function-signature → parameter-clause asyncopt throwsopt function-result opt
///     function-signature → parameter-clause asyncopt rethrows function-result opt
///     function-result → -> attributes opt type
///     function-body → code-block
///     parameter-clause → ( ) | ( parameter-list )
///     parameter-list → parameter | parameter , parameter-list
///     parameter → external-parameter-name opt local-parameter-name type-annotation default-argument-clause opt
///     parameter → external-parameter-name opt local-parameter-name type-annotation
///     parameter → external-parameter-name opt local-parameter-name type-annotation ...
///     external-parameter-name → identifier
///     local-parameter-name → identifier
///     default-argument-clause → = expression
class FunctionModel: Tokenizable, Commentable {

    var lineId: String?
    var attributes: AttributesModel
    var modifiers: DeclarationModifiersModel
    var accessLevel: AccessLevelModifier
    var name: String
    var genericParamClause: GenericParameterModel?
    var genericWhereClause: GenericWhereModel?
    var signature: SignatureModel

    init(from decl: FunctionDeclaration) {
        // FIXME: Fix this!
        lineId = "" // defId(forName: decl.fullName, withPrefix: defIdPrefix)
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = decl.accessLevel ?? .internal
        name = decl.name.textDescription
        genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        signature = SignatureModel(from: decl.signature)
    }

    init(from decl: ProtocolDeclaration.MethodMember) {
        // FIXME: Fix this!
        lineId = "" // defId(forName: decl.fullName, withPrefix: defIdPrefix)
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = modifiers.accessLevel ?? .internal
        name = decl.name.textDescription
        genericParamClause = GenericParameterModel(from: decl.genericParameter)
        genericWhereClause = GenericWhereModel(from: decl.genericWhere)
        signature = SignatureModel(from: decl.signature)
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        guard publicModifiers.contains(accessLevel) else { return t }
        t.append(contentsOf: attributes.tokenize())
        t.append(contentsOf: modifiers.tokenize())
        t.keyword("func")
        t.whitespace()
        t.member(name: name, definitionId: lineId)
        t.append(contentsOf: genericParamClause?.tokenize() ?? [])
        t.append(contentsOf: signature.tokenize())
        t.append(contentsOf: genericWhereClause?.tokenize() ?? [])
        t.newLine()
        return t
    }
}
