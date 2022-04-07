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
///     typealias-declaration → attributes opt access-level-modifier opt typealias typealias-name generic-parameter-clause opt typealias-assignment
///     typealias-name → identifier
///     typealias-assignment → = type
class TypealiasModel: Tokenizable, Linkable, Commentable, AccessLevelProtocol {

    var definitionId: String?
    var lineId: String?
    var parent: Linkable?
    var attributes: AttributesModel
    var accessLevel: AccessLevelModifier
    var name: String
    var genericParamClause: GenericParameterModel?
    var assignment: TypeModel

    init(from decl: TypealiasDeclaration, parent: Linkable) {
        self.parent = parent
        definitionId = identifier(forName: decl.name.textDescription, withPrefix: parent.definitionId)
        lineId = nil
        attributes = AttributesModel(from: decl.attributes)
        accessLevel = decl.accessLevel ?? .internal
        name = decl.name.textDescription
        genericParamClause = GenericParameterModel(from: decl.generic)
        assignment = decl.assignment.toTokenizable()!
    }

    func tokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        attributes.tokenize(apiview: a)
        a.keyword(accessLevel.textDescription, postfixSpace: true)
        a.keyword("typealias", postfixSpace: true)
        a.typeDeclaration(name: name, definitionId: definitionId)
        genericParamClause?.tokenize(apiview: a)
        a.punctuation("=", prefixSpace: true, postfixSpace: true)
        assignment.tokenize(apiview: a)
        a.newline()
        a.blankLines(set: 1)
    }

    func navigationTokenize(apiview a: APIViewModel) {
        a.add(token: NavigationToken(name: name, prefix: parent?.name, typeKind: .class))
    }
}
