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

/// Enum for token kind
enum TokenKind: Int, Codable {
    /// Text
    case text = 0
    /// Newline
    case newline = 1
    /// Whitespace
    case whitespace = 2
    /// Punctuation
    case punctuation = 3
    /// Swift keyword
    case keyword = 4
    /// used to display comment marker on lines with no visible tokens
    case lineIdMarker = 5
    /// Swift type name (class, structs, enums, etc.)
    case typeName = 6
    /// Variable names
    case memberName = 7
    /// Constants
    case stringLiteral = 8
}

/// An individual token item
struct TokenItem: Codable {
    // Allows tokens to be navigated to. Should be unique. Used as ID for comment thread.
    var definitionId: String?
    // If set, clicking on the token would navigate to the other token with this ID.
    var navigateToId: String?
    // Text value
    var value: String?
    // Token kind
    var kind: TokenKind

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case definitionId = "DefinitionId"
        case navigateToId = "NavigateToId"
        case value = "Value"
        case kind = "Kind"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(definitionId, forKey: .definitionId)
        try container.encode(navigateToId, forKey: .navigateToId)
        try container.encode(value, forKey: .value)
        try container.encode(kind, forKey: .kind)
    }
}
