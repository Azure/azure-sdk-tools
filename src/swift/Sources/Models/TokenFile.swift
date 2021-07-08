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

/// The token file that is readable by APIView
class TokenFile: Codable {
    /// The name to be used in the APIView review list
    var name: String
    /// List of APIVIew tokens to render
    var tokens: [TokenItem]
    /// List of APIView navigation items which display in the sidebar
    var navigation: [NavigationItem]

    var indentLevel: Int
    // MARK: Initializers

    init(name: String) {
        self.name = name
        self.tokens = []
        self.navigation = []
        self.indentLevel = 0
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case name = "Name"
        case tokens = "Tokens"
        case navigation = "Navigation"
        case indentLevel = "indentLevel"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(tokens, forKey: .tokens)
        try container.encode(navigation, forKey: .navigation)
        try container.encode(0, forKey: .indentLevel)
    }

    // MARK: Methods

    func addText(_ text: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: text, kind: .text)
        tokens.append(item)
    }

    func addNewline() {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
        tokens.append(item)
        if indentLevel > 0 {
            addWhitespace(spaces: indentLevel)
        }
    }

    func addWhitespace(spaces: Int = 1) {
        let value = String(repeating: " ", count: spaces)
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
        tokens.append(item)
    }

    func addPunctuation(_ value: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
        tokens.append(item)
    }

    func addKeyword(value: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .keyword)
        tokens.append(item)
    }

    func addLineIdMarker() {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .lineIdMarker)
        tokens.append(item)
    }

    func addType(name: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: name, kind: .typeName)
        tokens.append(item)
    }

    func addMember(name: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: name, kind: .memberName)
        tokens.append(item)
    }

    func addStringLiteral(_ text: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: text, kind: .stringLiteral)
        tokens.append(item)
    }
}

