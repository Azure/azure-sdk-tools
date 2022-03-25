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
class StructModel: Tokenizable, Navigable, Linkable {

    var definitionId: String?
    var attributes: AttributesModel
    var accessLevel: String
    var name: String
    var genericParamClause: GenericParameterModel?
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?
    var children: [Tokenizable]

    init(from decl: StructDeclaration) {
        self.attributes = AttributesModel(from: decl.attributes)
        self.accessLevel = ""
        self.name = ""
        self.definitionId = ""
        self.genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        self.typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        self.genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        self.children = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                // FIXME: The big switch statement again??
                break
            case .compilerControl(_):
                break
            }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
//        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
//        guard publicModifiers.contains(accessLevel) else { return false }

        // register type as linkable
//        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)
        t.append(contentsOf: attributes.tokenize())
        t.keyword(accessLevel)
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
        children.forEach { child in
            t.append(contentsOf: child.tokenize())
        }
        t.punctuation("}")
        t.newLine()
        return t
    }

    func navigationTokenize() -> [NavigationToken] {
        var t = [NavigationToken]()
        //    private func navigationTokens(from decl: StructDeclaration, withPrefix prefix: String) -> NavigationItem? {
        //        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
        //            return nil
        //        }
        //        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .struct)
        //        decl.members.forEach { member in
        //            switch member {
        //            case let .declaration(decl):
        //                if let item = navigationTokens(from: decl, withPrefix: navItem.navigationId) {
        //                    navItem.childItems.append(item)
        //                }
        //            case .compilerControl:
        //                return
        //            }
        //        }
        //        return navItem
        //    }
        return t
    }
}
