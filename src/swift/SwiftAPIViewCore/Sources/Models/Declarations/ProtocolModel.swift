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
///     protocol-declaration → attributes opt access-level-modifier opt protocol protocol-name type-inheritance-clause opt generic-where-clause opt protocol-body
///     protocol-name → identifier
///     protocol-body → { protocol-members opt }
///     protocol-members → protocol-member protocol-members opt
///     protocol-member → protocol-member-declaration | compiler-control-statement
///     protocol-member-declaration → protocol-property-declaration
///     protocol-member-declaration → protocol-method-declaration
///     protocol-member-declaration → protocol-initializer-declaration
///     protocol-member-declaration → protocol-subscript-declaration
///     protocol-member-declaration → protocol-associated-type-declaration
///     protocol-member-declaration → typealias-declaration
class ProtocolModel: Tokenizable, Navigable, Linkable {

    var definitionId: String?
    var attributes: AttributesModel
    var accessLevel: String
    var name: String
    var typeInheritanceClause: TypeInheritanceModel?
    var children: [Tokenizable]

    init(from decl: ProtocolDeclaration) {
        // FIXME: This!
        // self.definitionId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)
        self.attributes = AttributesModel(from: decl.attributes)
        self.accessLevel = ""
        self.name = ""
        self.typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        self.children = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
                case let .associatedType(data):
//                    let name = data.name.textDescription
//                    let defId = self.defId(forName: name, withPrefix: defId)
//                    keyword(value: "associatedtype")
//                    whitespace()
//                    self.member(name: name, definitionId: defId)
//                    if let inheritance = data.typeInheritance {
//                        handle(clause: inheritance, defId: defId)
//                    }
//                    newLine()
                    break
                case let .method(data):
//                    let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
//                    let defId = self.defId(forName: data.fullName, withPrefix: defId)
//                    let name = data.name.textDescription
//                    _ = processFunction(name: name, defId: defId, attributes: data.attributes, modifiers: data.modifiers, accessLevel: accessLevel, signature: data.signature, genericParam: data.genericParameter, genericWhere: data.genericWhere)
                    break
                case let .property(data):
//                    let name = data.name.textDescription
//                    let defId = self.defId(forName: name, withPrefix: defId)
//                    handle(attributes: data.attributes, defId: defId)
//                    handle(modifiers: data.modifiers)
//                    keyword(value: "var")
//                    whitespace()
//                    self.member(name: name, definitionId: defId)
//                    punctuation(":")
//                    whitespace()
//                    handle(typeModel: TypeModel(from: data.typeAnnotation), defId: defId)
//                    handle(clause: data.getterSetterKeywordBlock)
                    break
                case let .initializer(data):
//                    let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
//                    let defId = self.defId(forName: data.fullName, withPrefix: defId)
//                    _ = processInitializer(defId: defId, attributes: data.attributes, modifiers: data.modifiers, kind: data.kind.textDescription, accessLevel: accessLevel,  genericParam: data.genericParameter, throwsKind: data.throwsKind, parameterList: data.parameterList, genericWhere: data.genericWhere)
                    break
                case let .subscript(data):
//                    let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
//                    let defId = self.defId(forName: "subscript", withPrefix: defId)
//                    _ = processSubscript(defId: defId, attributes: data.attributes, modifiers: data.modifiers, accessLevel: accessLevel, genericParam: data.genericParameter, parameterList: data.parameterList, resultType: data.resultType, genericWhere: data.genericWhere, getterSetterKeywordBlock: data.getterSetterKeywordBlock)
                    break
                default:
                    SharedLogger.fail("Unsupported protocol member: \(member)")
            }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
//        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
//        guard publicModifiers.contains(accessLevel) else { return false }

        t.append(contentsOf: attributes.tokenize())
        t.keyword(accessLevel)
        t.whitespace()
        t.keyword("protocol")
        t.whitespace()
        t.typeDeclaration(name: name, definitionId: definitionId)
        t.whitespace()
        t.append(contentsOf: typeInheritanceClause?.tokenize() ?? [])
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
        //        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
        //            return nil
        //        }
        //        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .interface)
        //        for item in decl.members {
        //            if case let ProtocolDeclaration.Member.associatedType(data) = item {
        //                navItem.childItems.append(NavigationItem(name: data.name.textDescription, prefix: navItem.navigationId, typeKind: .class))
        //            }
        //        }
        return t
    }
}
