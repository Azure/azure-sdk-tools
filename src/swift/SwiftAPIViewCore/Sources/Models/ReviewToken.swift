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
    /// plain text tokens for e.g documentation, namespace value, or attribute or decorator tokens. Most tokens will be text
    case text = 0
    /// punctuation
    case punctuation = 1
    /// language-specific keywords like `class`
    case keyword = 2
    /// class definitions, base class token, parameter types etc
    case typeName = 3
    /// method name tokens, member variable tokens
    case memberName = 4
    /// metadata or string literals to show in API view
    case stringLiteral = 5
    /// literals, for e.g. enum value or numerical constant literal or default value
    case literal = 6
    /// Comment text within the code that's really a documentation. Few languages wants to show comments within
    /// API review that's not tagged as documentation.
    case comment = 7
}

/// An individual token item
class ReviewToken: Codable {
    /// Text value
    var value: String
    /// Token kind
    var kind: TokenKind
    /// used to create a tree node in the navigation panel. Navigation nodes will be created only if this is set.
    var navigationDisplayName: String?
    /// navigate to the associated `lineId` when this token is clicked. (e.g. a param type which is class name in
    /// the same package)
    var navigateToId: String?
    /// set to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency versions
    /// are usually excluded when comparing two revisions to avoid reporting them as API changes
    var skipDiff: Bool? = false
    /// set if API is marked as deprecated
    var isDeprecated: Bool? = false
    /// set to false if there is no suffix space required before next token. For e.g, punctuation right after method name
    var hasSuffixSpace: Bool? = true
    /// set to true if there is a prefix space required before current token. For e.g, space before token for =
    var hasPrefixSpace: Bool? = false
    /// set to true if current token is part of documentation
    var isDocumentation: Bool? = false
    /// Language-specific style css class names
    var renderClasses: [String]?

    init(value: String, kind: TokenKind) {
        self.value = value
        self.kind = kind
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case value = "Value"
        case kind = "Kind"
        case navigationDisplayName = "NavigationDisplayName"
        case navigateToId = "NavigateToId"
        case skipDiff = "SkipDiff"
        case isDeprecated = "IsDeprecated"
        case hasSuffixSpace = "HasSuffixSpace"
        case hasPrefixSpace = "HasPrefixSpace"
        case isDocumentation = "IsDocumentation"
        case renderClasses = "RenderClasses"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(value, forKey: .value)
        try container.encode(kind.rawValue, forKey: .kind)
        try container.encodeIfPresent(navigationDisplayName, forKey: .navigationDisplayName)
        try container.encodeIfPresent(navigateToId, forKey: .navigateToId)
        try container.encodeIfPresent(skipDiff, forKey: .skipDiff)
        try container.encodeIfPresent(isDeprecated, forKey: .isDeprecated)
        try container.encodeIfPresent(hasSuffixSpace, forKey: .hasSuffixSpace)
        try container.encodeIfPresent(hasPrefixSpace, forKey: .hasPrefixSpace)
        try container.encodeIfPresent(isDocumentation, forKey: .isDocumentation)
        try container.encodeIfPresent(renderClasses, forKey: .renderClasses)
    }

    var text: String {
        return value
    }
}

extension Array<ReviewToken> {
    var lastVisible: TokenKind? {
        var values = self
        while !values.isEmpty {
            if let item = values.popLast() {
                return item.kind
            }
        }
        return nil
    }
}
