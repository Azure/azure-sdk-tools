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
//
//    /// Adds a token to the list
//    internal func add(token: Token) {
//        tokens.append(token)
//    }
//
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
//    /// This should be the only process method that APIViewManager should be able to call
//    internal func process(statements: [Statement]) {
//        let bundle = Bundle(for: Swift.type(of: self))
//        let versionKey = "CFBundleShortVersionString"
//        let apiViewVersion = bundle.object(forInfoDictionaryKey: versionKey) as? String ?? "Unknown"
//        let defId = defId(forName: packageName, withPrefix: nil)
//        text("Package parsed using Swift APIView (version \(apiViewVersion))")
//        newLine()
//        newLine()
//        text("package")
//        whitespace()
//        text(packageName, definitionId: defId)
//        whitespace()
//        punctuation("{")
//        newLine()
//        indent {
//            let stopIdx = statements.count - 1
//            for (idx, statement) in statements.enumerated() {
//                switch statement {
//                case let decl as Declaration:
//                    if process(decl, defIdPrefix: defId), idx != stopIdx {
//                        // add an blank line for each declaration that is actually rendered
//                        newLine()
//                    }
//                default:
//                    SharedLogger.fail("Unsupported statement type: \(statement)")
//                }
//            }
//        }
//        punctuation("}")
//        newLine()
//
//    }
//
//
//
//
//
//
//    /// Returns false if declaration is skipped. True if it is processed.
//    private func process(_ decl: Declaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
//        switch decl {
//        case let decl as ClassDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as ConstantDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as EnumDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as ExtensionDeclaration:
//            let extensionPrefix = "\(defIdPrefix).\(decl.type.textDescription)"
//            return process(decl, defIdPrefix: extensionPrefix, overridingAccess: overridingAccess)
//        case let decl as FunctionDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as InitializerDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as ProtocolDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as StructDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as TypealiasDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as VariableDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case _ as ImportDeclaration:
//            // Imports are no-op
//            return false
//        case _ as DeinitializerDeclaration:
//            // Deinitializers are never public
//            return false
//        case let decl as SubscriptDeclaration:
//            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
//        case let decl as PrecedenceGroupDeclaration:
//            // precedence groups are always public
//            return process(decl, defIdPrefix: defIdPrefix)
//        case let decl as OperatorDeclaration:
//            // operators are always public
//            return process(decl, defIdPrefix: defIdPrefix)
//        default:
//            SharedLogger.fail("Unsupported declaration: \(decl)")
//        }
//    }
//
//    private func handle(async: AsyncKind) {
//        switch `async` {
//        case .async:
//            keyword(value: `async`.textDescription)
//            whitespace()
//        default:
//            return
//        }
//    }
//
//    private func handle(throws: ThrowsKind) {
//        switch `throws` {
//        case .throwing, .rethrowing:
//            keyword(value: `throws`.textDescription)
//            whitespace()
//        default:
//            return
//        }
//    }
//
//    private func handle(result: Type?, defId: String) {
//        guard let result = result else { return }
//        let typeModel = TypeModel(from: result)
//        punctuation("->")
//        whitespace()
//        handle(typeModel: typeModel, defId: defId)
//    }
//
//    private func handle(parameter: FunctionSignature.Parameter, defId: String) {
//        let name = parameter.externalName?.textDescription ?? parameter.localName.textDescription
//        let typeModel = TypeModel(from: parameter.typeAnnotation)
//        member(name: name)
//        punctuation(":")
//        whitespace()
//        handle(typeModel: typeModel, defId: defId)
//        if let defaultArgument = parameter.defaultArgumentClause {
//            whitespace()
//            punctuation("=")
//            whitespace()
//            stringLiteral(defaultArgument.textDescription)
//        }
//        if parameter.isVarargs {
//            text("...")
//        }
//    }
//
//    private func handle(modifiers: DeclarationModifiers) {
//        guard !modifiers.isEmpty else { return }
//        modifiers.forEach { modifier in
//            keyword(value: modifier.textDescription)
//            whitespace()
//        }
//    }
//
//    private func handle(signature: FunctionSignature, defId: String) {
//        punctuation("(")
//        if !signature.parameterList.isEmpty {
//            let stopIdx = signature.parameterList.count - 1
//            for (idx, parameter) in signature.parameterList.enumerated() {
//                handle(parameter: parameter, defId: defId)
//                if idx != stopIdx {
//                    punctuation(",")
//                    whitespace()
//                }
//            }
//        }
//        punctuation(")")
//        whitespace()
//        handle(async: signature.asyncKind)
//        handle(throws: signature.throwsKind)
//        handle(result: signature.result?.type, defId: defId)
//        whitespace()
//    }
//
//
//    private func handle(clause: GetterSetterKeywordBlock?) {
//        guard let clause = clause else { return }
//        whitespace()
//        punctuation("{")
//        whitespace()
//        if clause.getter.mutationModifier != nil {
//            keyword(value: "mutating")
//            whitespace()
//        }
//        keyword(value: "get" )
//        whitespace()
//        if let setter = clause.setter {
//            if setter.mutationModifier != nil {
//                keyword(value: "mutating")
//                whitespace()
//            }
//            keyword(value: "set")
//            whitespace()
//        }
//        punctuation("}")
//        newLine()
//    }
//
//
//    private func handle(tuple: TupleType?, defId: String) {
//        guard let tuple = tuple else { return }
//        punctuation("(")
//        let stopIdx = tuple.elements.count - 1
//        for (idx, element) in tuple.elements.enumerated() {
//            if let name = element.name?.textDescription {
//                member(name: name)
//                punctuation(":")
//                whitespace()
//            }
//            handle(typeModel: TypeModel(from: element.type), defId: defId)
//            if idx != stopIdx {
//                punctuation(",")
//                whitespace()
//            }
//        }
//        punctuation(")")
//    }
//
//
//    private func handle(member: StructDeclaration.Member, defId: String, overridingAccess: AccessLevelModifier? = nil) {
//        switch member {
//        case let .declaration(decl):
//            _ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
//        default:
//            SharedLogger.fail("Unsupported member: \(member)")
//        }
//    }
