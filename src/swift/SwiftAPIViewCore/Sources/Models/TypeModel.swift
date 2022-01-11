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
    var genericArgumentList = [TypeModel]()
    var attributes: Attributes? = nil

    init(from source: TypeAnnotation) {
        self.init(from: source.type)
        attributes = source.attributes
    }

    init(from source: OptionalType) {
        self.init(from: source.wrappedType)
        isOptional = true
    }

    init(from source: ArrayType) {
        self.init(from: source.elementType)
        isArray = true
    }

    init(from source: String) {
        name = source
    }

    init(from source: TypeIdentifier) {
        let genericArgumentClauses = source.names.compactMap { $0.genericArgumentClause }
        guard genericArgumentClauses.count < 2 else {
            SharedLogger.fail("Unexpectedly found multiple generic argument clauses.")
        }
        let names = source.names.map { $0.name.textDescription }
        if let clause = genericArgumentClauses.first {
            genericArgumentList = clause.argumentList.map { TypeModel(from: $0) }
        }
        name = String(names.joined(separator: "."))
    }

    init(from source: PatternInitializer) {
        if case let identPattern as IdentifierPattern = source.pattern,
            let typeAnnotation = identPattern.typeAnnotation {
            self.init(from: typeAnnotation)
        } else if let expression = source.initializerExpression as? LiteralExpression {
            self.init(from: expression.kind.textDescription)
        } else {
            SharedLogger.fail("Unable to extract type information.")
        }
    }

    init(from source: FunctionType) {
        // TODO: remove reliance on textDescription
        name = source.textDescription
        attributes = source.attributes
        let test = "best"
    }

    init(from source: DictionaryType) {
        // TODO: remove reliance on textDescription
        name = source.textDescription
        let test = "best"
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
        case let src as MetatypeType:
            self.init(from: src.referenceType)
        case let src as AnyType:
            self.init(from: src.textDescription)
        case let src as TupleType:
            self.init(from: src.textDescription)
        case let src as ImplicitlyUnwrappedOptionalType:
            self.init(from: src.textDescription)
        default:
            SharedLogger.fail("Unsupported identifier: \(source)")
        }
    }
}
