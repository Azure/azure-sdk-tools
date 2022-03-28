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

/// Conforming objects can be serialized into APIView tokens.
protocol Tokenizable {
    func tokenize() -> [Token]
}


extension Tokenizable {
    var publicModifiers: [AccessLevelModifier] {
        return [.public, .open]
    }
}

extension Array where Element == Token {

    mutating func add(token: Token) {
        append(token)
    }

    mutating func text(_ text: String, definitionId: String? = nil) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: nil, value: text, kind: .text)
        add(token: item)
    }

    mutating func newLine() {
        // strip trailing whitespace token, except blank lines
        if last?.kind == .whitespace {
            let popped  = self.popLast()!
            // lines that consist only of whitespace must be preserved
            if last?.kind == .newline {
                add(token: popped)
            }
        }
        checkIndent()
        let item = Token(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
        add(token: item)
        lineIdMarker()
        //needsIndent = true
    }

    mutating func whitespace(count: Int = 1) {
        // don't double up on whitespace
        guard last?.kind != .whitespace else { return }
        let value = String(repeating: " ", count: count)
        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
        add(token: item)
    }

    mutating func punctuation(_ value: String) {
        checkIndent()
        let item = Token(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
        add(token: item)
    }

    mutating func keyword(_ value: String, definitionId: String? = nil) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: nil, value: value, kind: .keyword)
        add(token: item)
    }

    mutating func lineIdMarker(definitionId: String? = nil) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: nil, value: nil, kind: .lineIdMarker)
        add(token: item)
    }

    /// Register the declaration of a new type
    mutating func typeDeclaration(name: String, definitionId: String? = nil) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: definitionId, value: name, kind: .typeName)
        add(token: item)
    }

    /// Link to a registered type
    mutating func typeReference(name: String) {
        checkIndent()
        // TODO: Ensure this works for dotted names
        // FIXME: fix this later. DefinitionIds should be stored elsewhere
        var definitionIds = Set<String>()
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

    mutating func member(name: String, definitionId: String? = nil) {
        checkIndent()
        let item = Token(definitionId: definitionId, navigateToId: nil, value: name, kind: .memberName)
        add(token: item)
    }

    mutating func stringLiteral(_ text: String) {
        checkIndent()
        let item = Token(definitionId: nil, navigateToId: nil, value: text, kind: .stringLiteral)
        add(token: item)
    }

    mutating func checkIndent() {
        // TODO: Implement
        return
    }

    mutating func indent(_ indentedCode: () -> Void) {
        // TODO: Fix this
        //indentLevel += 1
        indentedCode()
        //indentLevel -= 1
    }
}
