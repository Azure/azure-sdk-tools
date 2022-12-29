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


extension PatternInitializer {
    var name: String? {
        return (self.pattern as? IdentifierPattern)?.identifier.textDescription
    }

    var typeModel: TypeModel? {
        if case let typeAnno as IdentifierPattern = pattern,
            let typeInfo = typeAnno.typeAnnotation?.type {
            return typeInfo.toTokenizable()
        }
        if case let literalExpression as LiteralExpression = initializerExpression {
            return TypeIdentifierModel(name: literalExpression.kind.textDescription)
        }
        if case let functionExpression as FunctionCallExpression = initializerExpression {
            return TypeIdentifierModel(name: functionExpression.postfixExpression.textDescription)
        }
        return nil
    }

    var defaultValue: String? {
        // TODO: This only works for literal expressions. What about closures, etc? Do we care?
        return (initializerExpression as? LiteralExpression)?.textDescription
    }
}
