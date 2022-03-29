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
class EnumModel: Tokenizable, Linkable, Commentable, Extensible, AccessLevelProtocol {

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

            func tokenize(apiview a: APIViewModel) {
                if let name = name {
                    a.member(name: name)
                    a.punctuation(":", postfixSpace: true)
                }
                typeModel.tokenize(apiview: a)
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

        func tokenize(apiview a: APIViewModel) {
            a.punctuation("(")
            let stopIdx = elements.count - 1
            for (idx, element) in elements.enumerated() {
                element.tokenize(apiview: a)
                if idx != stopIdx {
                    a.punctuation(",", postfixSpace: true)
                }
            }
            a.punctuation(")")
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

        func tokenize(apiview a: APIViewModel) {
            a.keyword("case", postfixSpace: true)
            a.member(name: name, definitionId: lineId)
            associatedValues?.tokenize(apiview: a)
            a.newline()
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

        func tokenize(apiview a: APIViewModel) {
            a.keyword("case", postfixSpace: true)
            a.member(name: name, definitionId: lineId)
            if let value = rawValue {
                a.punctuation("=", prefixSpace: true, postfixSpace: true)
                a.literal(value)
            }
            a.newline()
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

    func tokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        attributes.tokenize(apiview: a)
        a.keyword(accessLevel.textDescription, postfixSpace: true)
        if isIndirect {
            a.keyword("indirect", postfixSpace: true)
        }
        a.keyword("enum", postfixSpace: true)
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

    func navigationTokenize(apiview a: APIViewModel, parent: Linkable?) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        a.add(token: NavigationToken(name: name, prefix: parent?.name, typeKind: .enum))
        for member in members {
            guard let member = member as? Linkable else { continue }
            member.navigationTokenize(apiview: a, parent: self)
        }
    }
}
