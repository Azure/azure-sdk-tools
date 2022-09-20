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


public struct FunctionTestClass {

    public func funcWithoutParams() -> String { "" }

    public func funcWithMultipleParams(person: String, alreadyGreeted: Bool) -> String { "" }

    public func funcWithoutReturnValue(person: String) { }

    public func funcWithReturnValue(string: String) -> Int { 1 }

    public func funcWithMultipleReturnValues(array: [Int]) -> (min: Int, max: Int)? { (0, 1) }

    public func funcWithArgumentLabels(argumentLabel parameterName: Int) { }

    public func funcWithoutArgumentLabels(_ firstParameterName: Int, _ secondParameterName: Int) { }

    public func funcWithDefaultValue(parameterWithoutDefault: Int, parameterWithDefault: Int = 12) { }

    public func funcWithVariadicParam(_ numbers: Double...) -> Double { 1.0 }

    public func funcWithInOutParams(_ a: inout Int, _ b: inout Int) { }

    public func addTwoInts(_ a: Int, _ b: Int) -> Int { a + b }

    public var mathFunction: (Int, Int) -> Int

    public func funcWithFuncTypeParam(_ mathFunction: (Int, Int) -> Int, _ a: Int, _ b: Int) { }

    public func funcWithFuncReturnType(backward: Bool) -> (Int) -> Int {
        func stepForward(input: Int) -> Int { return input + 1 }
        func stepBackward(input: Int) -> Int { return input - 1 }
        return backward ? stepBackward : stepForward
    }

    public func funcWithEscapingClosure(completionHandler: @escaping () -> Void) { }

    public func funcWithAutoclosureEscapingClosure(_ customerProvider: @autoclosure @escaping () -> String) { }
}
