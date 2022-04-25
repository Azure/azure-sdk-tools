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
///     precedence-group-declaration → precedencegroup precedence-group-name { precedence-group-attributes opt }
///     precedence-group-attributes → precedence-group-attribute precedence-group-attributes opt
///     precedence-group-attribute → precedence-group-relation
///     precedence-group-attribute → precedence-group-assignment
///     precedence-group-attribute → precedence-group-associativity
///     precedence-group-relation → higherThan : precedence-group-names
///     precedence-group-relation → lowerThan : precedence-group-names
///     precedence-group-assignment → assignment : boolean-literal
///     precedence-group-associativity → associativity : left
///     precedence-group-associativity → associativity : right
///     precedence-group-associativity → associativity : none
///     precedence-group-names → precedence-group-name | precedence-group-name , precedence-group-names
///     precedence-group-name → identifier
class PrecedenceGroupModel: Tokenizable, Commentable, Linkable {

    var lineId: String?
    var definitionId: String?
    var parent: Linkable?
    var name: String
    var members: [Tokenizable]

    struct AssociativityMember: Tokenizable, Commentable {

        var lineId: String?
        var value: String

        init(value: String, parent: PrecedenceGroupModel) {
            self.lineId = identifier(forName: "associativity", withPrefix: parent.definitionId)
            self.value = value
        }

        func tokenize(apiview a: APIViewModel) {
            a.lineIdMarker(definitionId: lineId)
            a.keyword("associativity")
            a.punctuation(":", postfixSpace: true)
            a.keyword(value)
        }
    }

    struct ComparisonMember: Tokenizable, Commentable {

        var lineId: String?
        var keyword: String
        var groups: [TypeModel]

        init(keyword: String, with identifiers: IdentifierList, parent: PrecedenceGroupModel) {
            self.lineId = identifier(forName: keyword, withPrefix: parent.definitionId)
            self.keyword = keyword
            self.groups = [TypeModel]()
            identifiers.forEach { item in
                self.groups.append(TypeIdentifierModel(name: item.textDescription))
            }
        }

        func tokenize(apiview a: APIViewModel) {
            a.lineIdMarker(definitionId: lineId)
            a.keyword(keyword)
            a.punctuation(":", postfixSpace: true)
            let stopIdx = groups.count - 1
            for (idx, group) in groups.enumerated() {
                group.tokenize(apiview: a)
                if idx != stopIdx {
                    a.punctuation(",", postfixSpace: true)
                }
            }
        }
    }

    struct AssignmentMember: Tokenizable, Commentable {

        var lineId: String?
        var value: Bool

        init(value: Bool, parent: PrecedenceGroupModel) {
            self.lineId = identifier(forName: "assignment", withPrefix: parent.definitionId)
            self.value = value
        }

        func tokenize(apiview a: APIViewModel) {
            a.lineIdMarker(definitionId: lineId)
            a.keyword("assignment")
            a.punctuation(":", postfixSpace: true)
            a.literal(String(value))
        }
    }

    init(from decl: PrecedenceGroupDeclaration, parent: Linkable) {
        self.parent = parent
        definitionId = identifier(forName: decl.name.textDescription, withPrefix: parent.definitionId)
        lineId = nil
        name = decl.name.textDescription
        members = [Tokenizable]()
        decl.attributes.forEach { item in
            switch item {
            case let .assignment(val):
                members.append(AssignmentMember(value: val, parent: self))
            case .associativityLeft:
                members.append(AssociativityMember(value: "left", parent: self))
            case .associativityNone:
                members.append(AssociativityMember(value: "none", parent: self))
            case .associativityRight:
                members.append(AssociativityMember(value: "right", parent: self))
            case let .higherThan(val):
                members.append(ComparisonMember(keyword: "higherThan", with: val, parent: self))
            case  let .lowerThan(val):
                members.append(ComparisonMember(keyword: "lowerThan", with: val, parent: self))
            }
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.keyword("precedencegroup", postfixSpace: true)
        a.typeDeclaration(name: name, definitionId: definitionId)
        a.punctuation("{", prefixSpace: true)
        a.newline()
        a.indent {
            members.forEach { member in
                member.tokenize(apiview: a)
                a.newline()
            }
        }
        a.punctuation("}")
        a.newline()
        a.blankLines(set: 1)
    }

    func navigationTokenize(apiview a: APIViewModel) {
        a.add(token: NavigationToken(name: name, prefix: parent?.name, typeKind: .unknown))
    }
}
