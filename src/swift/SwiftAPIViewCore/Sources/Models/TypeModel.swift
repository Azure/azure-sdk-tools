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

class TypeModel {
    var name: String
    var isOptional: Bool
    var isImplicitlyUnwrapped: Bool
    var isArray: Bool
    var isDict: Bool
    var isTuple: Bool
    var isInOut: Bool
    var genericArgumentList: [TypeModel]?
    var attributes: Attributes?
    var arguments: [TypeModel]?
    var returnType: TypeModel?

    init(name: String, isOptional: Bool = false, isImplicitlyUnwrapped: Bool = false, isArray: Bool = false, isDict: Bool = false, isTuple: Bool = false, isInOut: Bool = false, genericArgumentList: [TypeModel]? = nil, attributes: Attributes? = nil, arguments: [TypeModel]? = nil, returnType: TypeModel? = nil) {
        self.name = name
        self.isOptional = isOptional
        self.isImplicitlyUnwrapped = isImplicitlyUnwrapped
        self.isArray = isArray
        self.isDict = isDict
        self.isTuple = isTuple
        self.isInOut = isInOut
        self.genericArgumentList = genericArgumentList
        self.attributes = attributes
        self.arguments = arguments
        self.returnType = returnType
    }

    convenience init(from source: TypeAnnotation) {
        self.init(from: source.type)
        isInOut = source.isInOutParameter
        attributes = source.attributes
    }

    convenience init(from source: OptionalType) {
        self.init(from: source.wrappedType)
        isOptional = true
    }

    convenience init(from source: ArrayType) {
        self.init(
            name: "",
            isArray: true,
            arguments: [TypeModel(from: source.elementType)]
        )
    }

    convenience init(from source: TypeIdentifier) {
        let genericArgumentClauses = source.names.compactMap { $0.genericArgumentClause }
        guard genericArgumentClauses.count < 2 else {
            SharedLogger.fail("Unexpectedly found multiple generic argument clauses.")
        }
        let names = source.names.map { $0.name.textDescription }
        let name = String(names.joined(separator: "."))
        let genericArgs = genericArgumentClauses.first?.argumentList.map { TypeModel(from: $0) }
        if name == "Optional" {
            guard genericArgs?.count == 1 else {
                SharedLogger.fail("Optionals must have exactly one generic type.")
            }
            self.init(
                name: genericArgs!.first!.name,
                isOptional: true
            )
        } else if name == "Dictionary" {
            guard genericArgs?.count == 2 else {
                SharedLogger.fail("Dictionary must have exactly two generic arguments")
            }
            self.init(
                name: "",
                isDict: true,
                arguments: genericArgs
            )
        } else if name == "Array" {
            guard genericArgs?.count == 1 else {
                SharedLogger.fail("Array must have exactly one generic argument")
            }
            self.init(
                name: "",
                isArray: true,
                arguments: genericArgs
            )
        } else {
            self.init(
                name: name,
                genericArgumentList: genericArgs
            )
        }
    }

    convenience init(from source: PatternInitializer) {
        if case let identPattern as IdentifierPattern = source.pattern,
            let typeAnnotation = identPattern.typeAnnotation {
            self.init(from: typeAnnotation)
        } else if let expression = source.initializerExpression as? LiteralExpression {
            // TODO: Revist this
            self.init(name: expression.kind.textDescription)
        } else {
            SharedLogger.fail("Unable to extract type information.")
        }
    }

    convenience init(from source: FunctionType) {
        self.init(
            name: "",
            attributes: source.attributes,
            arguments: source.arguments.map { TypeModel(from: $0.type) },
            returnType: TypeModel(from: source.returnType)
        )
    }

    convenience init(from source: TupleType) {
        // FIXME: Handle named tuple names
        self.init(
            name: "",
            isTuple: true,
            arguments: source.elements.map { TypeModel(from: $0.type) }
        )
    }

    convenience init(from source: DictionaryType) {
        self.init(
            name: "",
            isDict: true,
            arguments: [TypeModel(from: source.keyType), TypeModel(from: source.valueType)]
        )
    }

    convenience init(from source: Type) {
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
        case let src as TupleType:
            self.init(from: src)
        case let src as ImplicitlyUnwrappedOptionalType:
            self.init(from: src.wrappedType)
            self.isImplicitlyUnwrapped = true
        case is AnyType:
            self.init(name: "Any")
        default:
            SharedLogger.fail("Unsupported identifier: \(source)")
        }
    }
}
