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

struct TypeModel {
    var name: String
    var isOptional = false
    var isArray = false

    init(from source: IdentifierPattern) {
        guard let typeAnnotation = source.typeAnnotation?.type else {
            SharedLogger.fail("Missing type annotation.")
        }
        self.init(from: typeAnnotation)
    }

    init(from source: TypeAnnotation) {
        self.init(from: source.type)
    }

    init(from source: OptionalType) {
        self.init(from: source.wrappedType)
        isOptional = true
    }

    init(from source: ArrayType) {
        self.init(from: source.elementType)
        isArray = true
    }

    init(from source: TypeIdentifier) {
        let genericArgumentClauses = source.names.compactMap { $0.genericArgumentClause }
        guard genericArgumentClauses.count < 2 else {
            SharedLogger.fail("Unexpectedly found multiple generic argument clauses.")
        }
        if genericArgumentClauses.count == 1 {
            // TODO: remove reliance on textDescription
            name = source.textDescription
        } else {
            name = source.names.map { $0.name.textDescription }.joined(separator: ".")
        }
    }

    init(from source: FunctionType) {
        // TODO: remove reliance on textDescription
        name = source.textDescription
    }

    init(from source: DictionaryType) {
        // TODO: remove reliance on textDescription
        name = source.textDescription
    }

    init(from source: Type) {
        switch source {
        case let src as OptionalType:
            self.init(from: src)
        case let src as TypeIdentifier:
            self.init(from: src)
        case let src as ArrayType:
            self.init(from: src)
        case let src as FunctionType:
            self.init(from: src)
        case let src as DictionaryType:
            self.init(from: src)
        default:
            SharedLogger.fail("Unsupported identifier: \(source)")
        }
    }
}
