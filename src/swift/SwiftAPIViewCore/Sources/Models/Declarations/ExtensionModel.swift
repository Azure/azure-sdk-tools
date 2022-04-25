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


class ExtensionModel: Tokenizable, Commentable {

    var lineId: String?
    var attributes: AttributesModel
    var accessLevel: AccessLevelModifier?
    var typeModel: TypeModel
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?
    var members: [AccessLevelProtocol]

    init(from decl: ExtensionDeclaration, parent: Linkable) {
        lineId = identifier(forName: decl.type.textDescription, withPrefix: parent.definitionId)
        attributes = AttributesModel(from: decl.attributes)
        accessLevel = decl.accessLevel
        self.typeModel = decl.type.toTokenizable()!
        typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        members = [AccessLevelProtocol]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                // TODO: We need to push these into the relevant object definitions.
                // We should not just use the Extension's parent
                if var model = decl.toTokenizable(withParent: parent) as? AccessLevelProtocol {
                    // a public extension implicitly makes all its members public as well
                    if let overrideAccess = accessLevel {
                        model.accessLevel = overrideAccess
                    }
                    self.members.append(model)
                }
            case let .compilerControl(statement):
                SharedLogger.warn("Unsupported compiler control statement: \(statement)")
            }
        }
    }

    var isPublic: Bool {
        guard let accessLevel = accessLevel else { return false }
        return APIViewModel.publicModifiers.contains(accessLevel)
    }

    var hasPublicMembers: Bool {
        for member in members {
            if APIViewModel.publicModifiers.contains(member.accessLevel) {
                return true
            }
        }
        return false
    }

    func tokenize(apiview a: APIViewModel) {
        let shouldDisplay = isPublic || hasPublicMembers
        guard shouldDisplay == true else { return }
        a.lineIdMarker(definitionId: lineId)
        attributes.tokenize(apiview: a)
        if let access = accessLevel {
            a.keyword(access.textDescription, postfixSpace: true)
        }
        a.keyword("extension", postfixSpace: true)
        typeModel.tokenize(apiview: a)
        typeInheritanceClause?.tokenize(apiview: a)
        genericWhereClause?.tokenize(apiview: a)
        a.punctuation("{", prefixSpace: true)
        a.newline()
        a.indent {
            members.forEach { member in
                member.tokenize(apiview: a)
            }
        }
        a.punctuation("}")
        a.newline()
        a.blankLines(set: 1)
    }
}
