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


class CodeModel: Tokenizable, Encodable {

    /// The package name used by APIView
    var packageName: String

    /// The package version
    var packageVersion: String

    /// Version of the APIView language parser used to create the token file
    let parserVersion: String

    /// Language discriminator
    let language = "Swift"

    /// Only applicable currently to Java language variants
    let languageVariant: String? = nil

    /// The cross-language ID of the package
    let crossLanguagePackageId: String?

    /// The top level review lines for the APIView
    var reviewLines: [ReviewLine]

    /// System generated comments. Each is linked to a review line ID.
    var diagnostics: [CodeDiagnostic]?

    /// Node-based representation of the Swift package
    private var model: PackageModel

    /// Access modifier to expose via APIView
    static let publicModifiers: [AccessLevel] = [.public, .open]

    /// sentinel value for unresolved type references
    static let unresolved = "__UNRESOLVED__"

    /// sentinel value for missing IDs
    static let missingId  = "__MISSING__"

    /// Tracks assigned definition IDs so they can be linked
    private var definitionIds = Set<String>()

    /// Stores the current line. All helper methods append to this.
    internal var currentLine: ReviewLine

    /// Stores the parent of the current line.
    internal var currentParent: ReviewLine? = nil

    /// Stores the stack of parent lines
    private var parentStack: [ReviewLine] = []

    /// Used to track the currently processed namespace.
    private var namespaceStack = NamespaceStack()

    /// Keeps track of which types have been declared.
    private var typeDeclarations = Set<String>()

    /// Returns the text-based representation of all lines
    var text: String {
        var value = ""
        for line in reviewLines {
            let lineText = line.text()
            value += lineText
        }
        return value
    }
    
    // MARK: Initializers

    init(packageName: String, packageVersion: String, statements: [CodeBlockItemSyntax.Item]) {
        // Renders the APIView "preamble"
        let bundle = Bundle(for: Swift.type(of: self))
        let versionKey = "CFBundleShortVersionString"
        self.packageVersion = packageVersion
        self.packageName = packageName
        self.parserVersion = bundle.object(forInfoDictionaryKey: versionKey) as? String ?? "Unknown"
        self.currentLine = ReviewLine()
        reviewLines = [ReviewLine]()
        diagnostics = [CodeDiagnostic]()
        model = PackageModel(name: packageName, statements: statements)
        // FIXME: Actually wire this up!
        self.crossLanguagePackageId = nil
        self.tokenize(apiview: self, parent: nil)
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case packageName = "PackageName"
        case packageVersion = "PackageVersion"
        case parserVersion = "ParserVersion"
        case language = "Language"
        case languageVariant = "LanguageVariant"
        case crossLanguagePackageId = "CrossLanguagePackageId"
        case reviewLines = "ReviewLines"
        case diagnostics = "Diagnostics"
        case navigation = "Navigation"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(packageName, forKey: .packageName)
        try container.encode(packageVersion, forKey: .packageVersion)
        try container.encode(parserVersion, forKey: .parserVersion)
        try container.encode(language, forKey: .language)
        try container.encode(languageVariant, forKey: .languageVariant)
        try container.encode(reviewLines, forKey: .reviewLines)
        try container.encodeIfPresent(crossLanguagePackageId, forKey: .crossLanguagePackageId)
        try container.encodeIfPresent(diagnostics, forKey: .diagnostics)
    }

    func tokenize(apiview a: CodeModel, parent: Linkable?) {
        self.text("Package parsed using Swift APIView (version \(self.parserVersion))")
        self.newline()
        self.blankLines(set: 2)
        model.tokenize(apiview: a, parent: parent)
    }

    // MARK: Token Emitters
    func token(kind: TokenKind, value: String, options: ReviewTokenOptions? = nil) {
        var options = options ?? ReviewTokenOptions()
        // workaround the silly server default
        options.hasSuffixSpace = options.hasSuffixSpace != nil ? options.hasSuffixSpace : false
        let token = ReviewToken(kind: kind, value: value, options: options)
        self.currentLine.tokens.append(token)
        return
    }

    func token(_ token: ReviewToken) {
        self.currentLine.tokens.append(token)
        return
    }

    func text(_ text: String, options: ReviewTokenOptions? = nil) {
        self.token(kind: .text, value: text, options: options)
    }

    func newline() {
        let text = self.currentLine.text()
        // ensure no trailing space at the end of the line
        if self.currentLine.tokens.count > 0 {
            let lastToken = currentLine.tokens.last
            lastToken?.hasSuffixSpace = false
            let firstToken = currentLine.tokens.first
            firstToken?.hasPrefixSpace =  false
        }

        if let currentParent = self.currentParent {
            currentParent.children.append(self.currentLine)
        } else {
            self.reviewLines.append(self.currentLine)
        }
        self.currentLine = ReviewLine()
    }

    /// Set the exact number of desired newlines.
    func blankLines(set count: Int) {
        self.newline()
        var parentLines = self.currentParent?.children ?? self.reviewLines
        // count the number of trailing newlines
        var newlineCount = 0
        for line in parentLines.reversed() {
            if line.tokens.count == 0 {
                newlineCount += 1
            } else {
                break
            }
        }
        if newlineCount == count {
            return
        } else if (newlineCount > count) {
            // if there are too many newlines, remove some
            let linesToRemove = newlineCount - count
            for _ in 0..<linesToRemove {
                if currentParent != nil {
                    _ = currentParent!.children.popLast()
                } else {
                    _ = self.reviewLines.popLast()
                } 
            }
        } else {
            // if not enough newlines, add some
            let linesToAdd = count - newlineCount
            for _ in 0..<linesToAdd {
                self.newline()
            }
        }
    }

    func punctuation(_ value: String, options: ReviewTokenOptions? = nil) {
        let token = ReviewToken(kind: .punctuation, value: value, options: options)
        if value == "}" {
            self.snap(token: token, to: "{")
            self.currentLine.isContextEndLine = true
        } else if value == "(" {
            // Remove suffix space if preceding token is text
            if let lastToken = currentLine.tokens.last {
                let shorten = ["get", "set", "willSet", "didSet"]
                if shorten.contains(lastToken.value) {
                    lastToken.hasSuffixSpace = false
                }
            }
            self.token(token)
        } else {
            self.token(token)
        }
    }

    func keyword(_ keyword: String, options: ReviewTokenOptions? = nil) {
        self.token(kind: .keyword, value: keyword, options: options)
    }

    func lineMarker(_ value: String? = nil, options: LineMarkerOptions? = nil) {
        self.currentLine.lineId = value ?? self.namespaceStack.value()
        if options?.addCrossLanguageId == true {
            self.currentLine.crossLanguageId = value ?? self.namespaceStack.value()
        }
        self.currentLine.relatedToLine = options?.relatedLineId
    }

    /// Register the declaration of a new type
    func typeDeclaration(name: String, typeId: String?, addCrossLanguageId: Bool = false, options: ReviewTokenOptions? = nil) {
        if let typeId = typeId {
            if self.typeDeclarations.contains(typeId) {
                fatalError("Duplicate ID '\(typeId)' for declaration will result in bugs.")
            }
            self.typeDeclarations.insert(typeId)
        }
        self.lineMarker(typeId, options: LineMarkerOptions(addCrossLanguageId: true))
        self.token(kind: .typeName, value: name, options: options)
    }

//    // FIXME: This!
//    func findNonFunctionParent(from item: DeclarationModel?) -> DeclarationModel? {
//        guard let item = item else { return nil }
//        switch item.kind {
//        case .method:
//            // look to the parent of the function
//            return findNonFunctionParent(from: (item.parent as? DeclarationModel))
//        default:
//            return item
//        }
//    }

    /// Link to a registered type
    func typeReference(name: String, options: ReviewTokenOptions? = nil) {
        var newOptions = options ?? ReviewTokenOptions()
        newOptions.navigateToId = options?.navigateToId ?? CodeModel.missingId
        self.token(kind: .typeName, value: name, options: newOptions)
    }

//    // FIXME: This!
//    func definitionId(for val: String, withParent parent: DeclarationModel?) -> String? {
//        var matchVal = val
//        if !matchVal.contains("."), let parentObject = findNonFunctionParent(from: parent), let parentDefId = parentObject.definitionId {
//            // if a plain, undotted name is provided, try to append the parent prefix
//            matchVal = "\(parentDefId).\(matchVal)"
//        }
//        let matches: [String]
//        if matchVal.contains(".") {
//            matches = definitionIds.filter { $0.hasSuffix(matchVal) }
//        } else {
//            // if type does not contain a dot, then suffix is insufficient
//            // we must completely match the final segment of the type name
//            matches = definitionIds.filter { $0.split(separator: ".").last! == matchVal }
//        }
//        if matches.count > 1 {
//            SharedLogger.warn("Found \(matches.count) matches for \(matchVal). Using \(matches.first!). Swift APIView may not link correctly.")
//        }
//        return matches.first
//    }

    func member(name: String, options: ReviewTokenOptions? = nil) {
        self.token(kind: .memberName, value: name, options: options)
    }

    func diagnostic(message: String, targetId: String, level: CodeDiagnosticLevel, helpLink: String?) {
        let diagnostic = CodeDiagnostic(message, targetId: targetId, level: level, helpLink: helpLink)
        self.diagnostics = self.diagnostics ?? []
        self.diagnostics?.append(diagnostic)
    }

    func comment(_ text: String, options: ReviewTokenOptions? = nil) {
        var message = text
        if !text.starts(with: "\\") {
            message = "\\\\ \(message)"
        }
        self.token(kind: .comment, value: message, options: options)
    }

    func literal(_ value: String, options: ReviewTokenOptions? = nil) {
        self.token(kind: .literal, value: value, options: options)
    }

    func stringLiteral(_ text: String, options: ReviewTokenOptions? = nil) {
        let lines = text.split(separator: "\n")
        if lines.count > 1 {
            let token = ReviewToken(kind: .stringLiteral, value: "\u{0022}\(text)\u{0022}", options: options)
            self.token(token)
        } else {
            self.punctuation("\u{0022}\u{0022}\u{0022}", options: options)
            self.newline()
            for line in lines {
                self.literal(String(line), options: options)
                self.newline()
            }
            self.punctuation("\u{0022}\u{0022}\u{0022}", options: options)
        }
    }

    /// Wraps code in indentattion
    func indent(_ indentedCode: () -> Void) {
        // ensure no trailing space at the end of the line
        let lastToken = self.currentLine.tokens.last
        lastToken?.hasSuffixSpace = false

        if let currentParent = self.currentParent {
            currentParent.children.append(self.currentLine)
            self.parentStack.append(currentParent)
        } else {
            self.reviewLines.append(self.currentLine)
        }
        self.currentParent = self.currentLine
        self.currentLine = ReviewLine()

        // handle the indented bodies
        indentedCode()

        // handle the de-indent logic
        guard let currentParent = self.currentParent else {
            fatalError("Cannot de-indent without a parent")
        }
        // ensure that the last line before the deindent has no blank lines
        if let lastChild = currentParent.children.popLast() {
            if (lastChild.tokens.count > 0) {
                currentParent.children.append(lastChild)
            }
        }
        self.currentParent = self.parentStack.popLast()
        self.currentLine = ReviewLine()
    }

    // FIXME: This!
    /// Constructs a definition ID and ensures it is unique.
    func defId(forName name: String, withPrefix prefix: String?) -> String {
        var defId = prefix != nil ? "\(prefix!).\(name)" : name
        defId = defId.filter { !$0.isWhitespace }
        if self.definitionIds.contains(defId) {
            SharedLogger.warn("Duplicate definition ID: \(defId). APIView will display duplicate comments.")
        }
        definitionIds.insert(defId)
        return defId
    }

    /// Places the provided token in the tree based on the provided characters.
    func snap(token target: ReviewToken, to characters: String) {
        let allowed = Set(characters)
        let lastLine = self.getLastLine() ?? self.currentLine

        // iterate through tokens in reverse order
        for token in lastLine.tokens.reversed() {
            switch token.kind {
            case .text:
                // skip blank whitespace tokens
                let value = token.value.trimmingTrailingCharacters(in: .whitespaces)
                if value.count == 0 {
                    continue
                } else {
                    // no snapping, so render in place
                    self.token(target)
                    return
                }
            case .punctuation:
                // ensure no whitespace after the trim character
                if allowed.first(where: { String($0) == token.value }) != nil {
                    token.hasSuffixSpace = false
                    target.hasSuffixSpace = false
                    lastLine.tokens.append(target)
                    return
                } else {
                    // no snapping, so render in place
                    self.token(target)
                    return
                }
            default:
                // no snapping, so render in place
                self.token(target)
                return
            }
        }
    }

    /// Retrieves the last line from the review
    func getLastLine() -> ReviewLine? {
        var current: ReviewLine? = nil
        if let currentParent = self.currentParent {
            current = currentParent.children.last
        } else if let lastReviewLine = self.reviewLines.last {
            current = lastReviewLine
        }
        if let lastChild = current?.children.last {
            current = lastChild
        }
        return current
    }
}
