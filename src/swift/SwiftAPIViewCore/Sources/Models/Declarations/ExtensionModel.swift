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


class ExtensionModel: Tokenizable {

    var attributes: AttributesModel
    var accessLevel: String?
    var typeModel: TypeModel
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?
    var members: [Tokenizable]

    init(from decl: ExtensionDeclaration) {
        self.attributes = AttributesModel(from: decl.attributes)
        // FIXME: Update
        self.accessLevel = nil
        self.typeModel = TypeModel(from: decl.type)
        self.typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        self.genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        self.members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                // FIXME: Big ol' Switch!?
                //_ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
                break
            case let .compilerControl(statement):
                SharedLogger.fail("Unsupported statement: \(statement)")
            }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        //        let accessLevel = decl.accessLevelModifier ?? overridingAccess
        //
        //        let shouldDisplay = decl.isPublic || decl.hasPublicMembers
        //        guard shouldDisplay == true else { return false }
        t.append(contentsOf: attributes.tokenize())
        if let access = accessLevel {
            t.keyword(access)
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
//        decl.members.forEach { member in
//            handle(member: member, defId: defIdPrefix, overridingAccess: accessLevel)
//        }
        t.punctuation("}")
        t.newLine()
        return t
    }

    //    private func navigationTokens(from decl: ExtensionDeclaration, withPrefix prefix: String) -> NavigationItem? {
    //        // FIXME: extensions should not have navigation tokens by themselves.
    //        // If the extension contains something that should be shown in navigation,
    //        // it should be associated with the type it extends.
    //        return nil
    //    }
}
