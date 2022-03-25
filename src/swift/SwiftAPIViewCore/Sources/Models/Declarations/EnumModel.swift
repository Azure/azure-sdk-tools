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
///     enum-declaration → attributes opt access-level-modifier opt union-style-enum
///     enum-declaration → attributes opt access-level-modifier opt raw-value-style-enum
///     union-style-enum → indirectopt enum enum-name generic-parameter-clause opt type-inheritance-clause opt generic-where-clause opt { union-style-enum-members opt }
///     union-style-enum-members → union-style-enum-member union-style-enum-members opt
///     union-style-enum-member → declaration | union-style-enum-case-clause | compiler-control-statement
///     union-style-enum-case-clause → attributes opt indirectopt case union-style-enum-case-list
///     union-style-enum-case-list → union-style-enum-case | union-style-enum-case , union-style-enum-case-list
///     union-style-enum-case → enum-case-name tuple-type opt
///     enum-name → identifier
///     enum-case-name → identifier
///     raw-value-style-enum → enum enum-name generic-parameter-clause opt type-inheritance-clause generic-where-clause opt { raw-value-style-enum-members }
///     raw-value-style-enum-members → raw-value-style-enum-member raw-value-style-enum-members opt
///     raw-value-style-enum-member → declaration | raw-value-style-enum-case-clause | compiler-control-statement
///     raw-value-style-enum-case-clause → attributes opt case raw-value-style-enum-case-list
///     raw-value-style-enum-case-list → raw-value-style-enum-case | raw-value-style-enum-case , raw-value-style-enum-case-list
///     raw-value-style-enum-case → enum-case-name raw-value-assignment opt
///     raw-value-assignment → = raw-value-literal
///     raw-value-literal → numeric-literal | static-string-literal | boolean-literal
class EnumModel: Tokenizable, Navigable, Linkable {

    var definitionId: String?
    var attributes: AttributesModel
    var accessLevel: String
    var name: String
    var members: [Tokenizable]
    var isIndirect: Bool
    var genericParamClause: GenericParameterModel?
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?

    init(from decl: EnumDeclaration) {
        self.attributes = AttributesModel(from: decl.attributes)
        self.isIndirect = decl.isIndirect
        self.accessLevel = ""
        self.name = decl.name.textDescription
        self.members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                // FIXME: Update
                break
                // _ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
            case let .union(enumCase):
                // FIXME: Update
                break
//                enumCase.cases.forEach { enumCaseValue in
//                    let enumDefId = self.defId(forName: enumCaseValue.name.textDescription, withPrefix: defId)
//                    keyword(value: "case")
//                    whitespace()
//                    self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
//                    handle(tuple: enumCaseValue.tuple, defId: enumDefId)
//                    newLine()
//                }
            case let .rawValue(enumCase):
                // FIXME: Update
                break
//                enumCase.cases.forEach { enumCaseValue in
//                    let enumDefId = self.defId(forName: enumCaseValue.name.textDescription, withPrefix: defId)
//                    keyword(value: "case")
//                    whitespace()
//                    self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
//                    if let value = enumCaseValue.assignment {
//                        whitespace()
//                        punctuation("=")
//                        whitespace()
//                        switch value {
//                        case let .boolean(val):
//                            stringLiteral(String(val))
//                        case let .floatingPoint(val):
//                            stringLiteral(String(val))
//                        case let .integer(val):
//                            stringLiteral(String(val))
//                        case let .string(val):
//                            stringLiteral("\"\(val)\"")
//                        }
//                    }
//                    newLine()
//                }
            default:
                SharedLogger.fail("Unsupported member: \(member)")
            }
        }
        self.genericParamClause = GenericParameterModel(from: decl.genericParameterClause)
        self.genericWhereClause = GenericWhereModel(from: decl.genericWhereClause)
        self.typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        //        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        //        guard publicModifiers.contains(accessLevel) else { return false }
        //
        //        // register type as linkable
        //        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)
        t.append(contentsOf: attributes.tokenize())
        t.keyword(accessLevel)
        t.whitespace()
        if isIndirect {
            t.keyword("indirect")
            t.whitespace()
        }
        t.keyword("enum")
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
        t.punctuation("}")
        t.newLine()
        return t
    }

    func navigationTokenize() -> [NavigationToken] {
        var t = [NavigationToken]()
//        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
//            return nil
//        }
//        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .enum)
//        decl.members.forEach { member in
//            switch member {
//            case let .declaration(memberDecl):
//                if let item = navigationTokens(from: memberDecl, withPrefix: navItem.navigationId) {
//                    navItem.childItems.append(item)
//                }
//            default:
//                return
//            }
//        }
        return t
    }
}
