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
    var throwing: ThrowsKind?
    var result: TypeModel?

    struct ParameterModel: Tokenizable {

        var name: String
        var attributes: AttributesModel?
        var typeModel: TypeAnnotationModel
        var defaultValue: String?
        var isVariadic: Bool

        init(from source: FunctionSignature.Parameter) {
            name = source.externalName?.textDescription ?? source.localName.textDescription
            if let attrs = source.attributes {
                attributes = AttributesModel(from: attrs)
            } else {
                attributes = nil
            }
            typeModel = TypeAnnotationModel(from: source.typeAnnotation)!
            // TODO: This may not be a good assumption
            defaultValue = source.defaultArgumentClause?.textDescription
            isVariadic = source.isVarargs
        }

        func tokenize(apiview a: APIViewModel) {
            attributes?.tokenize(apiview: a)
            a.member(name: name)
            a.punctuation(":", postfixSpace: true)
            typeModel.tokenize(apiview: a)
            if let defaultValue = defaultValue {
                a.punctuation("=", prefixSpace: true, postfixSpace: true)
                a.literal(defaultValue)
            }
            if isVariadic {
                a.text("...")
            }
        }
    }

    init(from sig: FunctionSignature) {
        switch sig.asyncKind {
        case .async:
            async = "async"
        case .notasync:
            async = nil
        }
        throwing = sig.throwsKind
        parameters = [ParameterModel]()
        sig.parameterList.forEach { param in
            parameters.append(ParameterModel(from: param))
        }
        if let resultType = sig.result?.type {
            result = resultType.toTokenizable()!
        } else {
            result = nil
        }
    }

    init(params: [FunctionSignature.Parameter], result: Type?, resultAttributes: Attributes?, throwsKind: ThrowsKind?) {
        async = nil
        throwing = throwsKind
        if let resultType = result {
            self.result = resultType.toTokenizable()!
        }
        parameters = [ParameterModel]()
        params.forEach { param in
            parameters.append(ParameterModel(from: param))
        }
    }

    func tokenize(apiview a: APIViewModel) {
        a.punctuation("(")
        let stopIdx = parameters.count - 1
        for (idx, param) in parameters.enumerated() {
            param.tokenize(apiview: a)
            if idx != stopIdx {
                a.punctuation(",", postfixSpace: true)
            }
        }
        a.punctuation(")", postfixSpace: true)
        if let async = async {
            a.keyword(async, postfixSpace: true)
        }
        throwing?.tokenize(apiview: a)
        if let result = result {
            a.punctuation("->", prefixSpace: true, postfixSpace: true)
            result.tokenize(apiview: a)
        }
        a.whitespace()
    }
}
