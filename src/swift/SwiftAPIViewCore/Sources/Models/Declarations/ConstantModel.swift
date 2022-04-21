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
///     constant-declaration → attributes opt declaration-modifiers opt let pattern-initializer-list
///     pattern-initializer-list → pattern-initializer | pattern-initializer , pattern-initializer-list
///     pattern-initializer → pattern initializer opt
///     initializer → = expression
class ConstantModel: Tokenizable, Commentable, AccessLevelProtocol {

    var lineId: String?
    var attributes: AttributesModel
    var modifiers: DeclarationModifiersModel
    var accessLevel: AccessLevelModifier
    var initializers: [InitializerItemModel]

    init(from decl: ConstantDeclaration, parent: Linkable) {
        initializers = decl.initializerList.compactMap { InitializerItemModel(from: $0) }
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = decl.accessLevel ?? .internal
        lineId = identifier(forName: initializers.first!.name, withPrefix: parent.definitionId)
    }

    func tokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        a.lineIdMarker(definitionId: lineId)
        attributes.tokenize(apiview: a)
        modifiers.tokenize(apiview: a)
        a.keyword("let", postfixSpace: true)
        let stopIdx = initializers.count - 1
        for (idx, item) in initializers.enumerated() {
            item.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.newline()
    }
}
