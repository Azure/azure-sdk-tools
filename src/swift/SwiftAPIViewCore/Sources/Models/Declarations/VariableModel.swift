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


/// Grammar Summary
///     variable-declaration → variable-declaration-head pattern-initializer-list
///     variable-declaration → variable-declaration-head variable-name type-annotation code-block
///     variable-declaration → variable-declaration-head variable-name type-annotation getter-setter-block
///     variable-declaration → variable-declaration-head variable-name type-annotation getter-setter-keyword-block
///     variable-declaration → variable-declaration-head variable-name initializer willSet-didSet-block
///     variable-declaration → variable-declaration-head variable-name type-annotation initializer opt willSet-didSet-block
///     variable-declaration-head → attributes opt declaration-modifiers opt var
///     variable-name → identifier
///     getter-setter-block → code-block
///     getter-setter-block → { getter-clause setter-clause opt }
///     getter-setter-block → { setter-clause getter-clause }
///     getter-clause → attributes opt mutation-modifier opt get code-block
///     setter-clause → attributes opt mutation-modifier opt set setter-name opt code-block
///     setter-name → ( identifier )
///     getter-setter-keyword-block → { getter-keyword-clause setter-keyword-clause opt }
///     getter-setter-keyword-block → { setter-keyword-clause getter-keyword-clause }
///     getter-keyword-clause → attributes opt mutation-modifier opt get
///     setter-keyword-clause → attributes opt mutation-modifier opt set
///     willSet-didSet-block → { willSet-clause didSet-clause opt }
///     willSet-didSet-block → { didSet-clause willSet-clause opt }
///     willSet-clause → attributes opt willSet setter-name opt code-block
///     didSet-clause → attributes opt didSet setter-name opt code-block
class VariableModel: Tokenizable, Commentable, AccessLevelProtocol {

    var lineId: String?
    var attributes: AttributesModel
    var modifiers: DeclarationModifiersModel
    var accessLevel: AccessLevelModifier
    var name: String
    var typeModel: TypeModel
    var defaultValue: String?
    var getterSetterBlock: GetterSetterModel?

    init(from decl: VariableDeclaration, parent: Linkable) {
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = decl.accessLevel ?? .internal
        getterSetterBlock = nil
        switch decl.body {
        case let .initializerList(initializerList):
            name = initializerList.name
            typeModel = initializerList.typeModel
            defaultValue = initializerList.defaultValue
        case let .codeBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        case let .getterSetterKeywordBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        case let .getterSetterBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        case let .willSetDidSetBlock(ident, typeAnno, expression, _):
            // the willSetDidSet block is irrelevant from an API perspective
            // so we ignore it.
            typeModel = TypeModel(from: typeAnno!)
            name = ident.textDescription
            guard expression == nil else {
                SharedLogger.fail("Expression not implemented. Please contact the SDK team.")
            }
        }
        lineId = identifier(forName: name, withPrefix: parent.definitionId)
    }

    init(from decl: ProtocolDeclaration.PropertyMember, parent: ProtocolModel) {
        let name = decl.name.textDescription
        self.name = name
        lineId = identifier(forName: name, withPrefix: parent.definitionId)
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = modifiers.accessLevel ?? .internal
        typeModel = TypeModel(from: decl.typeAnnotation)
        getterSetterBlock = GetterSetterModel(from: decl.getterSetterKeywordBlock)
    }

    func tokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        attributes.tokenize(apiview: a)
        modifiers.tokenize(apiview: a)
        a.keyword("var", postfixSpace: true)
        a.member(name: name, definitionId: lineId)
        a.punctuation(":", postfixSpace: true)
        typeModel.tokenize(apiview: a)
        if let defaultValue = defaultValue {
            a.punctuation("=", prefixSpace: true, postfixSpace: true)
            a.literal(defaultValue)
        }
        a.newline()
        getterSetterBlock?.tokenize(apiview: a)
    }
}
