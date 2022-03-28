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
class EnumModel: Tokenizable, Linkable, Commentable, Extensible {

    var definitionId: String?
    var lineId: String?
    var attributes: AttributesModel
    var accessLevel: AccessLevelModifier
    var name: String
    var isIndirect: Bool
    var genericParamClause: GenericParameterModel?
    var typeInheritanceClause: TypeInheritanceModel?
    var genericWhereClause: GenericWhereModel?
    var members: [Tokenizable]
    var extensions: [ExtensionModel]

    struct AssociatedValuesModel: Tokenizable {

        struct Element: Tokenizable {

            var name: String?
            var typeModel: TypeModel

            init(from element: TupleType.Element) {
                name = element.name?.textDescription
                typeModel = TypeModel(from: element)
            }

            func tokenize() -> [Token] {
                var t = [Token]()
                if let name = name {
                    t.member(name: name)
                    t.punctuation(":")
                    t.whitespace()
                }
                t.append(contentsOf: typeModel.tokenize())
                return t
            }
        }

        var elements: [Element]

        init?(from tuple: TupleType?) {
            guard let tuple = tuple else { return nil }
            elements = [Element]()
            tuple.elements.forEach { element in
                elements.append(Element(from: element))
            }
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.punctuation("(")
            let stopIdx = elements.count - 1
            for (idx, element) in elements.enumerated() {
                t.append(contentsOf: element.tokenize())
                if idx != stopIdx {
                    t.punctuation(",")
                    t.whitespace()
                }
            }
            t.punctuation(")")
            return t
        }
    }

    struct UnionStyleCase: Tokenizable, Commentable {

        var lineId: String?
        var name: String
        var associatedValues: AssociatedValuesModel?

        init(from source: EnumDeclaration.UnionStyleEnumCase.Case) {
            // FIXME: Fix this
            //let enumDefId = self.defId(forName: enumCaseValue.name.textDescription, withPrefix: defId)
            lineId = nil
            name = source.name.textDescription
            associatedValues = AssociatedValuesModel(from: source.tuple)
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.keyword("case")
            t.whitespace()
            t.member(name: name, definitionId: lineId)
            t.append(contentsOf: associatedValues?.tokenize() ?? [])
            t.newLine()
            return t
        }
    }

    struct RawValueCase: Tokenizable, Commentable {

        var lineId: String?
        var name: String
        var rawValue: String?

        init(from source: EnumDeclaration.RawValueStyleEnumCase.Case) {
            // FIXME: Fix this
            //let enumDefId = self.defId(forName: enumCaseValue.name.textDescription, withPrefix: defId)
            lineId = nil
            name = source.name.textDescription
            rawValue = nil
            if let assignment = source.assignment {
                switch assignment {
                case let .boolean(val):
                    rawValue = String(val)
                case let .floatingPoint(val):
                    rawValue = String(val)
                case let .integer(val):
                    rawValue = String(val)
                case let .string(val):
                    rawValue = "\"\(val)\""
                }
            }
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.keyword("case")
            t.whitespace()
            t.member(name: name, definitionId: lineId)
            if let value = rawValue {
                t.whitespace()
                t.punctuation("=")
                t.whitespace()
                t.stringLiteral(value)
            }
            t.newLine()
            return t
        }
    }

    init(from decl: EnumDeclaration) {
        // FIXME: Fix this!
        definitionId = nil // defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)
        lineId = nil
        attributes = AttributesModel(from: decl.attributes)
        isIndirect = decl.isIndirect
        accessLevel = decl.accessLevel ?? .internal
        name = decl.name.textDescription
        extensions = [ExtensionModel]()
        members = [Tokenizable]()
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let model = decl.toTokenizable() {
                    members.append(model)
                }
            case let .union(enumCase):
                enumCase.cases.forEach { item in
                    members.append(UnionStyleCase(from: item))
                }
            case let .rawValue(enumCase):
                enumCase.cases.forEach { item in
                    members.append(RawValueCase(from: item))
                }
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
        guard publicModifiers.contains(accessLevel) else { return t }
        t.append(contentsOf: attributes.tokenize())
        t.keyword(accessLevel.textDescription)
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
        t.append(NavigationToken(name: name, prefix: parent?.name, typeKind: .enum))
        for member in members {
            guard let member = member as? Linkable else { continue }
            t.append(contentsOf: member.navigationTokenize(parent: self))
        }
        return t
    }
}
