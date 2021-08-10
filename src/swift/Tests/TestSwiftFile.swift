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

    static public func ==(lhs: SomeClass<GenericType>, rhs: SomeClass<GenericType>) -> Bool {
        return false
    }

    deinit {
    }
}

// MARK: Test Protocol

public protocol Container {
    associatedtype Item
    mutating func append(_ item: Item)
    var count: Int { get }
    subscript(i: Int) -> Item { get }
}

public protocol SomeOtherProtocol {
    var mutatableValue: String { mutating get mutating set }

    func doSomething(param1: Int, _ param2: String...) throws -> String

    subscript(index: Int) -> String { get }

    init?(withName name: String) throws

    init!(withRisk risk: String)

    init(withRegular reg: String)
}

public final class ThirdClass: SomeOtherProtocol {
    public var mutatableValue: String

    public func doSomething(param1: Int, _ param2: String...) throws -> String {
        return ""
    }

    public subscript(index: Int) -> String {
        return ""
    }

    public init?(withName name: String = "Great Name!") throws {
        return nil
    }

    public init!(withRisk risk: String) {
        return nil
    }

    public init(withRegular reg: String) {
        mutatableValue = reg
    }

    public typealias SomeProtocolType = String

    public var value: String {
        return "value"
    }

    public func allItemsMatch<C1: Container, C2: Container>(_ someContainer: C1, _ anotherContainer: C2) -> Bool where C1.Item == C2.Item, C1.Item: Equatable {
        return false
    }
}

// MARK: Typealias

public typealias SomeClosure = (Result<String, Error>) -> Void

public func invoke(withClosure closure: @escaping SomeClosure) {
    closure(.success("succeeded"))
}

// MARK: Test Struct

@available(macOS 10.12, *)
public struct SomeStruct: Codable, Equatable {
    static var text = "Hello, World!"
    public static var staticVar = "initial value"
    public let const = "value"

    public func myMethod(takes type: Any) -> Any {
        return "" as Any
    }
}

// MARK: Test Generic Class

@available(macOS 10.12, *)
public class SomeGeneric<GenericType> {}

// MARK: Test Enum

@available(macOS 10.12, *)
public indirect enum VariableNode<GenericType> {
    case endpoint(value: GenericType)
    case node(value: GenericType, next: VariableNode)
    case vague(GenericType)
}

public enum RawValue: String {
    case first = "First"
    case second
}

// MARK: Test Extension

public extension SomeStruct {
    internal func someInternalMethod() throws {
        print("Doing stuff")
    }

    @discardableResult
    func somePublicMethod() throws -> String {
        return "value"
    }
}

extension SomeStruct {
    public func specialPublicMethod() {
        return
    }
}

public class SomeSubscriptable {
    public subscript(index: String) -> String {
        return "Value"
    }
}

// MARK: Test Precedence Group

precedencegroup SquareSumOperatorPrecedence {
    lowerThan: MultiplicationPrecedence
    higherThan: AdditionPrecedence
    associativity: left
    assignment: false
}

// MARK: Test Operators

infix operator +-: SquareSumOperatorPrecedence
prefix operator +++
postfix operator ---

// MARK: ObjC Class

@objcMembers
open class SomeObjCClass: NSObject {

    open func myFunc() -> String {
        return "mine"
    }

    public func doubleInPlace(number: inout Int) {
        number *= 2
    }
}
