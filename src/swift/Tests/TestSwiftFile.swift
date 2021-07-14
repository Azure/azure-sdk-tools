import Foundation
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

// MARK: Test Class

@available(macOS 10.12, *)
public class SomeClass<GenericType> where GenericType: Codable {

    public let const: String = "value"
    public var value: String {
        didSet {
            numberOfEdits += 1
        }
    }
    public private(set) var numberOfEdits = 0

    let internalConst: String = "value"
    var internalValue: String

    public init(value: String) {
        self.value = value
        self.internalValue = value
    }

    public convenience init?() {
        self.init(value: "value")
    }

    public func test(name: String) throws -> Bool {
        return true
    }

    deinit {
    }
}

// MARK: Test Protocol

public protocol SomeProtocol: AnyObject {
    associatedtype SomeProtocolType

    var value: String { get }
}

public final class ThirdClass: SomeProtocol {
    public typealias SomeProtocolType = String

    public var value: String {
        return "value"
    }
}

// MARK: Test Struct

@available(macOS 10.12, *)
public struct SomeStruct: Codable, Equatable {
    static var text = "Hello, World!"
    public let const = "value"
}

// MARK: Test Generic Class

@available(macOS 10.12, *)
public class SomeGeneric<GenericType> {}

// MARK: Test Enum

@available(macOS 10.12, *)
public indirect enum VariableNode<GenericType> {
    case endpoint(value: GenericType)
    case node(value: GenericType, next: VariableNode)
}

// MARK: Test Extension

public extension SomeStruct {
    func someMethod() throws {
        print("Doing stuff")
    }
}

public class SomeSubscriptable {
    public subscript(index: String) -> String {
        return "Value"
    }
}
