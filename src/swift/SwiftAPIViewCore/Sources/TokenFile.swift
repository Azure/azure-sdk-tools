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

import AST
import Foundation

///// The token file that is readable by APIView
//class TokenFile: Codable {
//    /// The name to be used in the APIView review list
//    var name: String
//
//    /// Package name for the review
//    var packageName: String
//
//    /// The version string
//    var versionString: String
//
//    /// Language string.
//    let language = "Swift"
//
//    /// List of APIVIew tokens to render
//    var tokens: [Token]
//
//    /// List of APIView navigation items which display in the sidebar
//    var navigation: [NavigationItem]
//
//    /// Indentation level
//    private var indentLevel = 0
//
//    /// Number of spaces per indentation level
//    private let indentSpaces = 4
//
//    /// Tracks whether indentation is needed
//    private var needsIndent = false
//
//    /// Access modifier to expose via APIView
//    let publicModifiers: [AccessLevelModifier] = [.public, .open]
//
//    /// Tracks assigned definition IDs so they can be linked
//    private var definitionIds = Set<String>()
//
//    var text: String {
//        return tokens.map { $0.text }.joined()
//    }
//
//    // MARK: Initializers
//
//    init(name: String, packageName: String, versionString: String) {
//        self.name = name
//        tokens = []
//        navigation = []
//        self.packageName = packageName
//        self.versionString = versionString
//    }
//
//    // MARK: Codable
//
//    enum CodingKeys: String, CodingKey {
//        case name = "Name"
//        case tokens = "Tokens"
//        case language = "Language"
//        case packageName = "PackageName"
//        case navigation = "Navigation"
//        case versionString = "VersionString"
//    }
//
//    func encode(to encoder: Encoder) throws {
//        var container = encoder.container(keyedBy: CodingKeys.self)
//        try container.encode(name, forKey: .name)
//        try container.encode(packageName, forKey: .packageName)
//        try container.encode(language, forKey: .language)
//        try container.encode(tokens, forKey: .tokens)
//        try container.encode(navigation, forKey: .navigation)
//        try container.encode(versionString, forKey: .versionString)
//    }
//
//    // MARK: Token Emitter Methods
//
//    func text(_ text: String, definitionId: String? = nil) {
//        checkIndent()
//        let item = Token(definitionId: definitionId, navigateToId: nil, value: text, kind: .text)
//        add(token: item)
//    }
//
//    func newLine() {
//        // strip trailing whitespace token, except blank lines
//        if tokens.last?.kind == .whitespace {
//            let popped  = tokens.popLast()!
//            // lines that consist only of whitespace must be preserved
//            if tokens.last?.kind == .newline {
//                add(token: popped)
//            }
//        }
//        checkIndent()
//        let item = Token(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
//        add(token: item)
//        lineIdMarker()
//        needsIndent = true
//    }
//
//    func whitespace(count: Int = 1) {
//        // don't double up on whitespace
//        guard tokens.last?.kind != .whitespace else { return }
//        let value = String(repeating: " ", count: count)
//        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
//        add(token: item)
//    }
//
//    func punctuation(_ value: String) {
//        checkIndent()
//        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
//        add(token: item)
//    }
//
//    func keyword(value: String, definitionId: String? = nil) {
//        checkIndent()
//        let item = Token(definitionId: definitionId, navigateToId: nil, value: value, kind: .keyword)
//        add(token: item)
//    }
//
//    func lineIdMarker(definitionId: String? = nil) {
//        checkIndent()
//        let item = Token(definitionId: definitionId, navigateToId: nil, value: nil, kind: .lineIdMarker)
//        add(token: item)
//    }
//
//    /// Register the declaration of a new type
//    func typeDeclaration(name: String, definitionId: String? = nil) {
//        checkIndent()
//        let item = Token(definitionId: definitionId, navigateToId: definitionId, value: name, kind: .typeName)
//        add(token: item)
//    }
//
//    /// Link to a registered type
//    func typeReference(name: String) {
//        checkIndent()
//        // TODO: Ensure this works for dotted names
//        let matches: [String]
//        if name.contains(".") {
//            matches = definitionIds.filter { $0.hasSuffix(name) }
//        } else {
//            // if type does not contain a dot, then suffix is insufficient
//            // we must completely match the final segment of the type name
//            matches = definitionIds.filter { $0.split(separator: ".").last! == name }
//        }
//        guard matches.count < 2 else {
//            SharedLogger.fail("Found \(matches.count) matches for \(name).")
//        }
//        let linkId = matches.first
//        let item = Token(definitionId: nil, navigateToId: linkId, value: name, kind: .typeName)
//        add(token: item)
//    }
//
//    func member(name: String, definitionId: String? = nil) {
//        checkIndent()
//        let item = Token(definitionId: definitionId, navigateToId: nil, value: name, kind: .memberName)
//        add(token: item)
//    }
//
//    func stringLiteral(_ text: String) {
//        checkIndent()
//        let item = Token(definitionId: nil, navigateToId: nil, value: text, kind: .stringLiteral)
//        add(token: item)
//    }
//
//    func indent(_ indentedCode: () -> Void) {
//        indentLevel += 1
//        indentedCode()
//        indentLevel -= 1
//    }
//
//    func checkIndent() {
//        guard needsIndent else { return }
//        whitespace(count: indentLevel * indentSpaces)
//        needsIndent = false
//    }
//
//    // MARK: Utility Methods
//    /// Constructs a definition ID and ensures it is unique.
//    internal func defId(forName name: String, withPrefix prefix: String?) -> String {
//        let defId = prefix != nil ? "\(prefix!).\(name)" : name
//        if defId.contains(" ") {
//            SharedLogger.fail("Definition ID should not contain whitespace: \(defId)")
//        }
//        if self.definitionIds.contains(defId) {
//            // FIXME: Change back to fail
//            SharedLogger.warn("Duplicate definition ID: \(defId). Will result in duplicate comments.")
//        }
//        definitionIds.insert(defId)
//        return defId
//    }
//
//    // MARK: Processing Methods
//
//    private func handle(result: Type?, defId: String) {
//        guard let result = result else { return }
//        let typeModel = TypeModel(from: result)
//        punctuation("->")
//        whitespace()
//        handle(typeModel: typeModel, defId: defId)
//    }
//
