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
import SwiftSyntax

class ReviewLine: Tokenizable, Encodable {
    /// Required to support commenting on a line. Usually code line for documentation or just punctuation is not required
    /// to have lineId. It is used to link comments and navigation to specific lines. It must be a unique value or logic bugs
    /// will manifest, but must also be deterministic (i.e. it cannot just be a random GUID).
    var lineId: String?
    /// whereas `lineId` is typically specific to the target language, `crossLanguageId` is the TypeSpec-based form of ID
    /// so that concepts can be linked across different languages. Like `lineId`, it must be unique and deterministic.
    var crossLanguageId: String?
    /// The list of tokens that forms this particular review line.
    var tokens: [ReviewToken] = []
    /// Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of a
    /// namespace(module) level code line. Similarly all method level code lines are added as children of it's class
    /// code line. Identation will automatically be applied to children.
    var children: [ReviewLine] = []
    /// Flag the line as hidden so it will not be shown to architects by default.
    var isHidden: Bool?
    /// Identifies that this line completes the existing context, usually the immediately previous reviewLine. For example,
    /// in a class defintion that uses curly braces, the context begins with the class definition line and the closing curly brace
    /// will be flagged as `isContextEndLine: True`.
    var isContextEndLine: Bool?
    /// Apply to any sibling-level `reviewLines` to mark that they are part of a specific context with the
    /// matching `lineId`. This is used for lines that may print above or within a context that are not indented.
    /// The final line of a context does not need this set. Instead, it should set `isContextEndLine`.
    var relatedToLine: String?

    /// Returns the text-based representation of all tokens
    func text(indent: Int = 0) -> String {
        let indentCount = 4
        let indentString = String(repeating: " ", count: indent)
        if tokens.count == 0 && children.count == 0 {
            return "\n"
        }
        var value = indentString
        for token in tokens {
            value += token.text(withPreview: value)
        }
        if tokens.count > 0 {
            value += "\n"
        }
        let childrenLines = self.children.map { $0.text(indent: indent + indentCount) }
        for line in childrenLines {
            value += line
        }
        return value
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case lineId = "LineId"
        case crossLanguageId = "CrossLanguageId"
        case tokens = "Tokens"
        case children = "Children"
        case isHidden = "IsHidden"
        case isContextEndLine = "IsContextEndLine"
        case relatedToLine = "RelatedToLine"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(tokens, forKey: .tokens)
        try container.encodeIfPresent(lineId, forKey: .lineId)
        try container.encodeIfPresent(crossLanguageId, forKey: .crossLanguageId)
        try container.encodeIfPresent(relatedToLine, forKey: .relatedToLine)
        if (!children.isEmpty) {
            try container.encode(children, forKey: .children)
        }
        if isHidden == true {
            try container.encode(isHidden, forKey: .isHidden)
        }
        if isContextEndLine == true {
            try container.encode(isContextEndLine, forKey: .isContextEndLine)
        }
    }

    // MARK: Tokenizable

    func tokenize(apiview: CodeModel, parent: (any Linkable)?) {
        fatalError("Not implemented!")
    }
}
