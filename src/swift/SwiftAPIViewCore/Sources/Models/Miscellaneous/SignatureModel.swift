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

class SignatureModel: Tokenizable {

    var parameters: [ParameterModel]
    var async: String?
    var throwing: String?
    var result: TypeModel?

    struct ParameterModel: Tokenizable {

        var name: String
        var typeModel: TypeModel
        var defaultValue: String?
        var isVarargs: Bool

        init(from source: FunctionSignature.Parameter) {
            name = source.externalName?.textDescription ?? source.localName.textDescription
            typeModel = TypeModel(from: source.typeAnnotation)
            // TODO: This may not be a good assumption
            defaultValue = source.defaultArgumentClause?.textDescription
            isVarargs = source.isVarargs
        }

        func tokenize() -> [Token] {
            var t = [Token]()
            t.member(name: name)
            t.punctuation(":")
            t.whitespace()
            t.append(contentsOf: typeModel.tokenize())
            if let defaultValue = defaultValue {
                t.whitespace()
                t.punctuation("=")
                t.whitespace()
                t.stringLiteral(defaultValue)
            }
            if isVarargs {
                t.text("...")
            }
            return t
        }
    }

    init(from sig: FunctionSignature) {
        switch sig.asyncKind {
        case .async:
            async = "async"
        case .notasync:
            async = nil
        }

        switch sig.throwsKind {
        case .throwing:
            throwing = "throws"
        case .nothrowing:
            throwing = nil
        case .rethrowing:
            throwing = "rethrow"
        }

        parameters = [ParameterModel]()
        sig.parameterList.forEach { param in
            parameters.append(ParameterModel(from: param))
        }
        if let resultType = sig.result?.type {
            result = TypeModel(from: resultType)
        } else {
            result = nil
        }
    }

    init(params: [FunctionSignature.Parameter], result: Type? = nil, resultAttributes: Attributes? = nil) {
        async = nil
        throwing = nil
        if let resultType = result {
            self.result = TypeModel(from: resultType)
        }
        parameters = [ParameterModel]()
        params.forEach { param in
            parameters.append(ParameterModel(from: param))
        }
    }

    func tokenize() -> [Token] {
        var t = [Token]()
        t.punctuation("(")
        let stopIdx = parameters.count - 1
        for (idx, param) in parameters.enumerated() {
            t.append(contentsOf: param.tokenize())
            if idx != stopIdx {
                t.punctuation(",")
                t.whitespace()
            }
        }
        t.punctuation(")")
        t.whitespace()
        if let async = async {
            t.keyword(async)
            t.whitespace()
        }
        if let throwing = throwing {
            t.keyword(throwing)
            t.whitespace()
        }
        if let result = result {
            t.append(contentsOf: result.tokenize())
        }
        t.whitespace()
        return t
    }
}
