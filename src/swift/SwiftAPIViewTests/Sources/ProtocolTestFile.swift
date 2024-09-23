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

// Protocol with different get/set levels

public protocol SomeProtocol {
    var mustBeSettable: Int { get set }
    var doesNotNeedToBeSettable: Int { get }
}

// Protocol with property requirements

public protocol FullyNamed {
    var fullName: String { get }
}

// Implementing protocol in declaration

public struct Person: FullyNamed {
    public var fullName: String
}

// Protocol with method requirements

public protocol RandomNumberGenerator {
    func random() -> Double
}

// Protocol with mutating function requirement

public protocol Togglable {
    mutating func toggle()
}

public enum OnOffSwitch: Togglable {
    case off, on
    public mutating func toggle() {
        switch self {
        case .off:
            self = .on
        case .on:
            self = .off
        }
    }
}

// Protocol with initializer requirements

public protocol SomeInitProtocol {
    init(someParameter: Int)
}

// Class implementation of protocol requirement

public class SomeClass: SomeInitProtocol {
    public required init(someParameter: Int) {
        // initializer implementation goes here
    }
}

// Subclass overriding protocol and superclass initializer

public protocol SomeOtherInitProtocol {
    init()
}

open class SomeSuperClass {
    public init() {
        // initializer implementation goes here
    }
}

public class SomeSubClass: SomeSuperClass, SomeOtherInitProtocol {
    // "required" from SomeProtocol conformance; "override" from SomeSuperClass
    public required override init() {
        // initializer implementation goes here
    }
}

// Protocol conformance with extension

public class Dice {
    public let sides: Int
    public let generator: RandomNumberGenerator
    public init(sides: Int, generator: RandomNumberGenerator) {
        self.sides = sides
        self.generator = generator
    }
    public func roll() -> Int {
        return Int(generator.random() * Double(sides)) + 1
    }
}

public protocol TextRepresentable {
    var textualDescription: String { get }
}

extension Dice: TextRepresentable {
    public var textualDescription: String {
        return "A \(sides)-sided dice"
    }
}

// Adding condition protocol conformance with extension

extension Array: TextRepresentable where Element: TextRepresentable {
    public var textualDescription: String {
        let itemsAsText = self.map { $0.textualDescription }
        return "[" + itemsAsText.joined(separator: ", ") + "]"
    }
}

// Declaring protocol adoption with extension

public struct Hamster {
    public var name: String
    public var textualDescription: String {
        return "A hamster named \(name)"
    }
}
extension Hamster: TextRepresentable {}

// Protocol inheritance

public protocol PrettyTextRepresentable: TextRepresentable {
    var prettyTextualDescription: String { get }
}

// Class-only protocols

public protocol SomeClassOnlyProtocol: AnyObject, TextRepresentable {
    // class-only protocol definition goes here
}

public protocol SomeOtherClassOnlyProtocol: class, TextRepresentable {
    // class-only protocol definition goes here
}

// Protocol composition

public protocol Named {
    var name: String { get }
}
public protocol Aged {
    var age: Int { get }
}
public struct ComposedPerson: Named, Aged {
    public var name: String
    public var age: Int
}
public func wishHappyBirthday(to celebrator: Named & Aged) {
    print("Happy birthday, \(celebrator.name), you're \(celebrator.age)!")
}

// Optional protocol requirements

@objc public protocol CounterDataSource {
    @objc optional func increment(forCount count: Int) -> Int
    @objc optional var fixedIncrement: Int { get }
}

// Protocol extensions

public extension RandomNumberGenerator {
    func randomBool() -> Bool {
        return random() > 0.5
    }
}

// Providing default implementations of protocol with extension
public extension PrettyTextRepresentable  {
    var prettyTextualDescription: String {
        return textualDescription
    }
}

// Adding constraints to protocol extensions

public extension Collection where Element: Equatable {
    func allEqual() -> Bool {
        for element in self {
            if element != self.first {
                return false
            }
        }
        return true
    }
}

