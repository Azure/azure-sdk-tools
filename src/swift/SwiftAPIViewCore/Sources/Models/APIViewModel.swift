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


class APIViewModel: Tokenizable, Encodable {

    /// The name to be used in the APIView review list
    var name: String

    /// The package name used by APIView
    var packageName: String

    /// The version string
    var versionString: String

    /// Language string.
    let language = "Swift"

    /// The APIView tokens to display
    var tokens: [Token]

    /// The navigation tokens to display
    var navigation: [NavigationToken]

    /// Node-based representation of the Swift package
    var model: PackageModel

    /// Current indentation level
    private var indentLevel = 0

    /// Whether indentation is needed
    private var needsIndent = false

    /// Number of spaces to indent per level
    let indentSpaces = 4

    /// Access modifier to expose via APIView
    // FIXME: Fix this!
    // static let publicModifiers: [AccessLevelModifierSyntax] = [.public, .open]

    /// Tracks assigned definition IDs so they can be linked
    private var definitionIds = Set<String>()

    /// Returns the text-based representation of all tokens
    var text: String {
        return tokens.map { $0.text }.joined()
    }
    
    // MARK: Initializers

    init(name: String, packageName: String, versionString: String, statements: [CodeBlockItemSyntax.Item]) {
        self.name = name
        self.versionString = versionString
        self.packageName = packageName
        navigation = [NavigationToken]()
        tokens = [Token]()
        model = PackageModel(name: packageName, statements: statements)
        self.tokenize(apiview: self)
        model.navigationTokenize(apiview: self)
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case name = "Name"
        case tokens = "Tokens"
        case language = "Language"
        case packageName = "PackageName"
        case navigation = "Navigation"
        case versionString = "VersionString"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(packageName, forKey: .packageName)
        try container.encode(language, forKey: .language)
        try container.encode(tokens, forKey: .tokens)
        try container.encode(navigation, forKey: .navigation)
        try container.encode(versionString, forKey: .versionString)
    }

    func tokenize(apiview a: APIViewModel) {
        // Renders the APIView "preamble"
        let bundle = Bundle(for: Swift.type(of: self))
        let versionKey = "CFBundleShortVersionString"
        let apiViewVersion = bundle.object(forInfoDictionaryKey: versionKey) as? String ?? "Unknown"
        a.text("Package parsed using Swift APIView (version \(apiViewVersion))")
        a.newline()
        a.blankLines(set: 2)
        model.tokenize(apiview: a)
    }

    // MARK: Token Emitters
    func add(token: Token) {
        self.tokens.append(token)
    }

    func add(token: NavigationToken) {
        self.navigation.append(token)
    }

    func text(_ text: String, definitionId: String? = nil) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: nil, value: text, kind: .text)
        // TODO: Add cross-language definition ID
        // if add_cross_language_id:
        //    token.cross_language_definition_id = self.metadata_map.cross_language_map.get(id, None)
        add(token: item)
    }

    /// Used to END a line and wrap to the next. Cannot be used to inject blank lines.
    func newline() {
        // strip trailing whitespace token, except blank lines
        if tokens.last?.kind == .whitespace {
            let popped  = tokens.popLast()!
            // lines that consist only of whitespace must be preserved
            if tokens.last?.kind == .newline {
                add(token: popped)
            }
        }
        checkIndent()
        // don't add newline if one already in place
        if tokens.last?.kind != .newline {
            let item = Token(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
            add(token: item)
        }
        needsIndent = true
    }

    /// Ensures a specific number of blank lines. Will add or remove newline
    /// tokens as needed to ensure the exact number of blank lines.
    func blankLines(set count: Int) {
        // count the number of trailing newlines
        var newlineCount = 0
        for token in self.tokens.reversed() {
            if token.kind == .newline {
                newlineCount += 1
            } else {
                break
            }
        }
        if newlineCount < (count + 1) {
            // if not enough newlines, add some
            let linesToAdd = (count + 1) - newlineCount
            for _ in 0..<linesToAdd {
                add(token: Token(definitionId: nil, navigateToId: nil, value: nil, kind: .newline))
            }
        } else if newlineCount > (count + 1) {
            // if there are too many newlines, remove some
            let linesToRemove = newlineCount - (count + 1)
            for _ in 0..<linesToRemove {
                _ = tokens.popLast()
            }
        }
        needsIndent = true
    }

    func whitespace(count: Int = 1) {
        // don't double up on whitespace
        guard tokens.last?.kind != .whitespace else { return }
        let value = String(repeating: " ", count: count)
        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
        add(token: item)
    }

    func punctuation(_ value: String, prefixSpace: Bool = false, postfixSpace: Bool=false) {
        checkIndent()
        if prefixSpace {
            self.whitespace()
        }
        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
        add(token: item)
        if postfixSpace {
            self.whitespace()
        }
    }

    func keyword(_ keyword: String, prefixSpace: Bool = false, postfixSpace: Bool = false) {
        checkIndent()
        if prefixSpace {
            self.whitespace()
        }
        let item = Token(definitionId: nil, navigateToId: nil, value: keyword, kind: .keyword)
        add(token: item)
        if postfixSpace {
            self.whitespace()
        }
    }

    /// Create a line ID marker (only needed if no other token has a definition ID)
    func lineIdMarker(definitionId: String?) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: nil, value: nil, kind: .lineIdMarker)
        add(token: item)
    }

    /// Register the declaration of a new type
    func typeDeclaration(name: String, definitionId: String?) {
        guard let definitionId = definitionId else {
            SharedLogger.fail("Type declaration '\(name)' does not have a definition ID.")
        }
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: definitionId, value: name, kind: .typeName)
        definitionIds.insert(definitionId)
        add(token: item)
    }

    /// Link to a registered type
    func typeReference(name: String) {
        checkIndent()
        // TODO: Ensure this works for dotted names
        let matches: [String]
        if name.contains(".") {
            matches = definitionIds.filter { $0.hasSuffix(name) }
        } else {
            // if type does not contain a dot, then suffix is insufficient
            // we must completely match the final segment of the type name
            matches = definitionIds.filter { $0.split(separator: ".").last! == name }
        }
        guard matches.count < 2 else {
            SharedLogger.fail("Found \(matches.count) matches for \(name).")
        }
        let linkId = matches.first
        let item = Token(definitionId: nil, navigateToId: linkId, value: name, kind: .typeName)
        add(token: item)
    }

    func member(name: String, definitionId: String? = nil) {
        let item = Token(definitionId: definitionId, navigateToId: nil, value: name, kind: .memberName)
        add(token: item)
    }

    // TODO: Add support for diagnostics
//    func diagnostic(self, text, line_id):
//        self.diagnostics.append(Diagnostic(line_id, text))

    func comment(_ text: String) {
        var message = text
        if !text.starts(with: "\\") {
            message = "\\\\ \(message)"
        }
        let item = Token(definitionId: nil, navigateToId: nil, value: message, kind: .comment)
        add(token: item)
    }

    func literal(_ value: String) {
        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .literal)
        add(token: item)
    }

    func stringLiteral(_ text: String) {
        let item = Token(definitionId: nil, navigateToId: nil, value: "\"\(text)\"", kind: .stringLiteral)
        add(token: item)
    }

    /// Wraps code in an indentation
    func indent(_ indentedCode: () -> Void) {
        indentLevel += 1
        indentedCode()
        // Don't end an indentation block with blank lines
        let tokenSuffix = Array(tokens.suffix(2))
        if tokenSuffix.count == 2 && tokenSuffix[0].kind == .newline && tokenSuffix[1].kind == .newline {
            _ = tokens.popLast()
        }
        indentLevel -= 1
    }

    /// Checks if indentation is needed and adds whitespace as needed
    func checkIndent() {
        guard needsIndent else { return }
        whitespace(count: indentLevel * indentSpaces)
        needsIndent = false
    }

    /// Constructs a definition ID and ensures it is unique.
    func defId(forName name: String, withPrefix prefix: String?) -> String {
        let defId = prefix != nil ? "\(prefix!).\(name)" : name
        if defId.contains(" ") {
            SharedLogger.fail("Definition ID should not contain whitespace: \(defId)")
        }
        if self.definitionIds.contains(defId) {
            // FIXME: Change back to fail when extensions are fixed
            SharedLogger.warn("Duplicate definition ID: \(defId). Will result in duplicate comments.")
        }
        definitionIds.insert(defId)
        return defId
    }
}
