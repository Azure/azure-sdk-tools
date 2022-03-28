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
/// struct-declaration → attributes opt access-level-modifier opt struct struct-name generic-parameter-clause opt type-inheritance-clause opt generic-where-clause opt struct-body
/// struct-name → identifier
/// struct-body → { struct-members opt }
/// struct-members → struct-member struct-members opt
/// struct-member → declaration | compiler-control-statement
class StructModel: Tokenizable, Linkable, Commentable, Extensible {

    var definitionId: String?
    var lineId: String?
    var attributes: AttributesModel
    var accessLevel: AccessLevelModifier
    var name: String
    var genericParamClause: GenericParameterModel?
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?
    var members: [Tokenizable]
    var extensions: [ExtensionModel]

    init(from decl: StructDeclaration) {
        // FIXME: Fix this!
        definitionId = nil // defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)
        lineId = nil
        attributes = AttributesModel(from: decl.attributes)
        accessLevel = decl.accessLevel ?? .internal
        name = decl.name.textDescription
        genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        extensions = [ExtensionModel]()
        members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let model = decl.toTokenizable() {
                    members.append(model)
                }
            case let .compilerControl(statement):
                SharedLogger.warn("Unsupported compiler control statement: \(statement)")
            }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        guard publicModifiers.contains(accessLevel) else { return t }
        t.append(contentsOf: attributes.tokenize())
        t.keyword(accessLevel.textDescription)
        t.whitespace()
        t.keyword("struct")
        t.whitespace()
        t.typeDeclaration(name: name, definitionId: definitionId)
        t.append(contentsOf: genericParamClause?.tokenize() ?? [])
        t.append(contentsOf: typeInheritanceClause?.tokenize() ?? [])
        t.append(contentsOf: genericWhereClause?.tokenize() ?? [])
        t.whitespace()
        t.punctuation("{")
        t.newLine()
        members.forEach { member in
            t.append(contentsOf: member.tokenize())
        }
        extensions.forEach { ext in
            t.append(contentsOf: ext.tokenize())
        }
        t.punctuation("}")
        t.newLine()
        return t
    }

    func navigationTokenize(parent: Linkable?) -> [NavigationToken] {
        var t = [NavigationToken]()
        guard publicModifiers.contains(accessLevel) else { return t }
        t.append(NavigationToken(name: name, prefix: parent?.name, typeKind: .struct))
        for member in members {
            guard let member = member as? Linkable else { continue }
            t.append(contentsOf: member.navigationTokenize(parent: self))
        }
        return t
    }
}
