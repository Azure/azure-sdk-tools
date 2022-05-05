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

extension Type {
    /// Convert a type  to a TypeTokenizable instance.
    func toTokenizable() -> TypeModel? {
        switch self {
        case let self as OptionalType:
            var model = self.wrappedType.toTokenizable()
            model?.optional = .optional
            return model
        case let self as ImplicitlyUnwrappedOptionalType:
            var model = self.wrappedType.toTokenizable()
            model?.optional = .implicitlyUnwrapped
            return model
        case let self as OpaqueType:
            var model = self.wrappedType.toTokenizable()
            model?.opaque = .opaque
            return model
        case let self as TypeIdentifier:
            return TypeIdentifierModel(from: self)
        case is AnyType:
            return TypeIdentifierModel(name: "Any")
        case is SelfType:
            return TypeIdentifierModel(name: "Self")
        case let self as ArrayType:
            return ArrayTypeModel(from: self)
        case let self as DictionaryType:
            return DictionaryTypeModel(from: self)
        case let self as FunctionType:
            return FunctionTypeModel(from: self)
        case let self as MetatypeType:
            return MetatypeTypeModel(from: self)
        case let self as TupleType:
            return TupleTypeModel(from: self)
        case let self as ProtocolCompositionType:
            return ProtocolCompositionModel(from: self)
        default:
            SharedLogger.fail("Unsupported identifier: \(self)")
        }
    }
}
