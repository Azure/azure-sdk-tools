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
///     class-declaration → attributes opt access-level-modifier opt finalopt class class-name generic-parameter-clause opt type-inheritance-clause opt generic-where-clause opt class-body
///     class-declaration → attributes opt final access-level-modifier opt class class-name generic-parameter-clause opt type-inheritance-clause opt generic-where-clause opt class-body
///     class-name → identifier
///     class-body → { class-members opt }
///     class-members → class-member class-members opt
///     class-member → declaration | compiler-control-statement
class ClassModel: Tokenizable, Linkable, Commentable, Extensible, AccessLevelProtocol {
    var definitionId: String?
    var lineId: String?
    var parent: Linkable?
    var attributes: AttributesModel
    var accessLevel: AccessLevelModifier
    var isFinal: Bool
    var name: String
    var genericParamClause: GenericParameterModel?
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?
    var members: [Tokenizable]
    var extensions: [ExtensionModel]

    init(from decl: ClassDeclaration, parent: Linkable) {
        self.parent = parent
        let name = decl.name.textDescription
        definitionId = identifier(forName: name, withPrefix: parent.definitionId)
        lineId = nil
        attributes = AttributesModel(from: decl.attributes)
        accessLevel = decl.accessLevel ?? .internal
        self.name = name
        isFinal = decl.isFinal
        genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        extensions = [ExtensionModel]()
        members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let model = decl.toTokenizable(withParent: self) {
                    if let model = model as? ExtensionModel {
                        // TODO: Place the extension in the appropriate location
                        extensions.append(model)
                    } else {
                        members.append(model)
                    }
                }
            case let .compilerControl(statement):
                SharedLogger.warn("Unsupported compiler control statement: \(statement)")
            }
        }
    }

    func tokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        attributes.tokenize(apiview: a)
        a.keyword(accessLevel.textDescription, postfixSpace: true)
        if isFinal {
            a.keyword("final", postfixSpace: true)
        }
        a.keyword("class", postfixSpace: true)
        a.typeDeclaration(name: name, definitionId: definitionId)
        genericParamClause?.tokenize(apiview: a)
        typeInheritanceClause?.tokenize(apiview: a)
        genericWhereClause?.tokenize(apiview: a)
        a.punctuation("{", prefixSpace: true)
        a.newline()
        a.indent {
            members.forEach { member in
                member.tokenize(apiview: a)
            }
            extensions.forEach { ext in
                ext.tokenize(apiview: a)
            }
        }
        a.punctuation("}")
        a.newline()
        a.blankLines(set: 1)
    }

    func navigationTokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        a.add(token: NavigationToken(name: name, prefix: parent?.name, typeKind: .class))
        for member in members {
            guard let member = member as? Linkable else { continue }
            member.navigationTokenize(apiview: a)
        }
    }
}
