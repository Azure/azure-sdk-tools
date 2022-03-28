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
    var name: String
    var members: [Tokenizable]

    struct AssociativityMember: Tokenizable, Commentable {

        var lineId: String?
        var value: String

        init(value: String) {
            // FIXME: Fix this!
            self.lineId = nil // self.defId(forName: "associativity", withPrefix: defId)
            self.value = value
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.keyword("associativity", definitionId: lineId)
            t.punctuation(":")
            t.whitespace()
            t.keyword(value)
            return t
        }
    }

    struct ComparisonMember: Tokenizable, Commentable {

        var lineId: String?
        var keyword: String
        var groups: [TypeModel]

        init(keyword: String, with identifiers: IdentifierList) {
            // FIXME: Fix this!
            self.lineId = nil // self.defId(forName: "higherThan", withPrefix: defId)
            self.keyword = keyword
            self.groups = [TypeModel]()
            identifiers.forEach { item in
                self.groups.append(TypeModel(from: item))
            }
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.keyword(keyword, definitionId: lineId)
            t.punctuation(":")
            t.whitespace()
            let stopIdx = groups.count - 1
            for (idx, group) in groups.enumerated() {
                t.append(contentsOf: group.tokenize())
                if idx != stopIdx {
                    t.punctuation(",")
                    t.whitespace()
                }
            }
            return t
        }
    }

    struct AssignmentMember: Tokenizable, Commentable {

        var lineId: String?
        var value: Bool

        init(value: Bool) {
            // FIXME: Fix this!
            self.lineId = nil
            self.value = value
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.keyword("assignment", definitionId: lineId)
            t.punctuation(":")
            t.whitespace()
            t.stringLiteral(String(value))
            return t
        }
    }

    init(from decl: PrecedenceGroupDeclaration) {
        // FIXME: Fix this!
        self.definitionId = nil
        self.lineId = nil
        self.name = decl.name.textDescription
        self.members = [Tokenizable]()
        decl.attributes.forEach { item in
            switch item {
            case let .assignment(val):
                self.members.append(AssignmentMember(value: val))
            case .associativityLeft:
                self.members.append(AssociativityMember(value: "left"))
            case .associativityNone:
                self.members.append(AssociativityMember(value: "none"))
            case .associativityRight:
                self.members.append(AssociativityMember(value: "right"))
            case let .higherThan(val):
                self.members.append(ComparisonMember(keyword: "higherThan", with: val))
            case  let .lowerThan(val):
                self.members.append(ComparisonMember(keyword: "lowerThan", with: val))
            }
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        t.keyword("precedencegroup")
        t.whitespace()
        t.typeDeclaration(name: name, definitionId: definitionId)
        t.whitespace()
        t.punctuation("{")
        t.newLine()
        members.forEach { item in
            t.append(contentsOf: item.tokenize())
            t.newLine()
        }
        t.punctuation("}")
        t.newLine()
        return t
    }

    func navigationTokenize(parent: Linkable?) -> [NavigationToken] {
        return [NavigationToken(name: name, prefix: parent?.name, typeKind: .unknown)]
    }
}
