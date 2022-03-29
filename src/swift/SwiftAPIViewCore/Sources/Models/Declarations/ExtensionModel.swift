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

    init(from decl: ExtensionDeclaration) {
        // FIXME: This this!
        self.lineId = nil
        self.attributes = AttributesModel(from: decl.attributes)
        self.accessLevel = decl.accessLevel
        self.typeModel = TypeModel(from: decl.type)
        self.typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        self.genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        self.members = [AccessLevelProtocol]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let model = decl.toTokenizable() as? AccessLevelProtocol {
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
        attributes.tokenize(apiview: a)
        if let access = accessLevel {
            a.keyword(access.textDescription, postfixSpace: true)
        }
        a.keyword("extension", postfixSpace: true)
        typeModel.tokenize(apiview: a)
        a.whitespace()
        typeInheritanceClause?.tokenize(apiview: a)
        genericWhereClause?.tokenize(apiview: a)
        a.punctuation("{")
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
