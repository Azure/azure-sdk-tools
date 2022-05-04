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
///     initializer-declaration → initializer-head generic-parameter-clause opt parameter-clause throwsopt generic-where-clause opt initializer-body
///     initializer-declaration → initializer-head generic-parameter-clause opt parameter-clause rethrows generic-where-clause opt initializer-body
///     initializer-head → attributes opt declaration-modifiers opt init
///     initializer-head → attributes opt declaration-modifiers opt init ?
///     initializer-head → attributes opt declaration-modifiers opt init !
///     initializer-body → code-block
class InitializerModel: Tokenizable, Commentable, AccessLevelProtocol {

    var lineId: String?
    var attributes: AttributesModel
    var modifiers: DeclarationModifiersModel
    var accessLevel: AccessLevelModifier
    var kind: String
    var name: String
    var genericParamClause: GenericParameterModel?
    var genericWhereClause: GenericWhereModel?
    var signature: SignatureModel

    init(from decl: InitializerDeclaration, parent: Linkable) {
        lineId = identifier(forName: decl.fullName, withPrefix: parent.definitionId)
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = decl.accessLevel ?? .internal
        name = "init"
        switch decl.kind {
        case .implicitlyUnwrappedFailable:
            kind = "!"
        case .optionalFailable:
            kind = "?"
        case .nonfailable:
            kind = ""
        }
        genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        signature = SignatureModel(params: decl.parameterList, result: nil, resultAttributes: nil)
    }

    init(from decl: ProtocolDeclaration.InitializerMember, parent: ProtocolModel) {
        lineId = identifier(forName: decl.fullName, withPrefix: parent.definitionId)
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = modifiers.accessLevel ?? .internal
        name = "init"
        switch decl.kind {
        case .implicitlyUnwrappedFailable:
            kind = "!"
        case .optionalFailable:
            kind = "?"
        case .nonfailable:
            kind = ""
        }
        genericParamClause = GenericParameterModel(from: decl.genericParameter)
        genericWhereClause = GenericWhereModel(from: decl.genericWhere)
        signature = SignatureModel(params: decl.parameterList, result: nil, resultAttributes: nil)
    }

    func tokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        attributes.tokenize(apiview: a)
        modifiers.tokenize(apiview: a)
        a.lineIdMarker(definitionId: lineId)
        a.keyword(name)
        a.punctuation(kind)
        genericParamClause?.tokenize(apiview: a)
        signature.tokenize(apiview: a)
        genericWhereClause?.tokenize(apiview: a)
        a.newline()
        a.blankLines(set: 1)
    }
}
