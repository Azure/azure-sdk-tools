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
class ProtocolModel: Tokenizable, Linkable, Commentable, Extensible {

    var definitionId: String?
    var lineId: String?
    var attributes: AttributesModel
    var accessLevel: AccessLevelModifier
    var name: String
    var typeInheritanceClause: TypeInheritanceModel?
    var members: [Tokenizable]
    var extensions: [ExtensionModel]

    struct AssociatedTypeModel: Tokenizable, Commentable {

        var lineId: String?
        var name: String
        var typeInheritance: TypeInheritanceModel?

        init(from source: ProtocolDeclaration.AssociativityTypeMember) {
            // FIXME: Fix this!
            lineId = nil // self.defId(forName: name, withPrefix: defId)
            name = source.name.textDescription
            typeInheritance = TypeInheritanceModel(from: source.typeInheritance)
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.keyword("associatedtype")
            t.whitespace()
            t.member(name: name, definitionId: lineId)
            t.append(contentsOf: typeInheritance?.tokenize() ?? [])
            t.newLine()
            return t
        }
    }

    init(from decl: ProtocolDeclaration) {
        // FIXME: Fix this!
        definitionId = nil // defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)
        lineId = nil
        attributes = AttributesModel(from: decl.attributes)
        accessLevel = decl.accessLevel ?? .internal
        name = decl.name.textDescription
        typeInheritanceClause = TypeInheritanceModel(from: decl.typeInheritanceClause)
        extensions = [ExtensionModel]()
        members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
                case let .associatedType(data):
                    members.append(AssociatedTypeModel(from: data))
                case let .method(data):
                    members.append(FunctionModel(from: data))
                case let .property(data):
                    members.append(VariableModel(from: data))
                case let .initializer(data):
                    members.append(InitializerModel(from: data))
                case let .subscript(data):
                    members.append(SubscriptModel(from: data))
                default:
                    SharedLogger.fail("Unsupported protocol member: \(member)")
            }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        guard publicModifiers.contains(accessLevel) else { return t }
        t.append(contentsOf: attributes.tokenize())
        t.keyword(accessLevel.textDescription)
        t.whitespace()
        t.keyword("protocol")
        t.whitespace()
        t.typeDeclaration(name: name, definitionId: definitionId)
        t.whitespace()
        t.append(contentsOf: typeInheritanceClause?.tokenize() ?? [])
        t.whitespace()
        t.punctuation("{")
        t.newLine()
        members.forEach { child in
            t.append(contentsOf: child.tokenize())
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
        t.append(NavigationToken(name: name, prefix: parent?.name, typeKind: .interface))
        for member in members {
            guard let member = member as? Linkable else { continue }
            t.append(contentsOf: member.navigationTokenize(parent: self))
        }
        return t
    }
}
