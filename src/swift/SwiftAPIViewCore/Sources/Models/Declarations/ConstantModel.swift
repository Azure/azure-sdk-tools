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
class ConstantModel: Tokenizable, Commentable {

    var lineId: String?
    var attributes: AttributesModel
    var modifiers: DeclarationModifiersModel
    var accessLevel: AccessLevelModifier
    var name: String
    var typeModel: TypeModel
    var defaultValue: String?

    init(from decl: ConstantDeclaration) {
        // FIXME: Fix this!
        lineId = nil
        attributes = AttributesModel(from: decl.attributes)
        modifiers = DeclarationModifiersModel(from: decl.modifiers)
        accessLevel = decl.accessLevel ?? .internal
        name = decl.initializerList.name
        typeModel = decl.initializerList.typeModel
        defaultValue = decl.initializerList.defaultValue
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        guard publicModifiers.contains(accessLevel) else { return t }
        t.append(contentsOf: attributes.tokenize())
        t.append(contentsOf: modifiers.tokenize())
        t.keyword("let")
        t.whitespace()
        t.member(name: name, definitionId: lineId)
        t.punctuation(":")
        t.whitespace()
        t.append(contentsOf: typeModel.tokenize())
        t.whitespace()
        t.punctuation("=")
        t.whitespace()
        if let defaultValue = defaultValue {
            t.stringLiteral(defaultValue)
        }
        t.newLine()
        return t
    }
}
