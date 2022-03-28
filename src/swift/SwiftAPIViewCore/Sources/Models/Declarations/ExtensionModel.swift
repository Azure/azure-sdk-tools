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
    var members: [Tokenizable]

    init(from decl: ExtensionDeclaration) {
        // FIXME: This this!
        self.lineId = nil
        self.attributes = AttributesModel(from: decl.attributes)
        self.accessLevel = decl.accessLevel
        self.typeModel = TypeModel(from: decl.type)
        self.typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        self.genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        self.members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let model = decl.toTokenizable() {
                    self.members.append(model)
                }
            case let .compilerControl(statement):
                SharedLogger.warn("Unsupported compiler control statement: \(statement)")
            }
        }
    }

    var isPublic: Bool {
        guard let accessLevel = accessLevel else { return false }
        return publicModifiers.contains(accessLevel)
    }

    var hasPublicMembers: Bool {
        members.forEach { member in
            // TODO: Cast to types with access level?
        }
        return false
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        let shouldDisplay = isPublic || hasPublicMembers
        guard shouldDisplay == true else { return t }
        t.append(contentsOf: attributes.tokenize())
        if let access = accessLevel {
            t.keyword(access.textDescription)
            t.whitespace()
        }
        t.keyword("extension")
        t.whitespace()
        t.append(contentsOf: typeModel.tokenize())
        t.whitespace()
        t.append(contentsOf: typeInheritanceClause?.tokenize() ?? [])
        t.append(contentsOf: genericWhereClause?.tokenize() ?? [])
        t.punctuation("{")
        t.newLine()
        members.forEach { member in
            t.append(contentsOf: member.tokenize())
        }
        t.punctuation("}")
        t.newLine()
        return t
    }
}
