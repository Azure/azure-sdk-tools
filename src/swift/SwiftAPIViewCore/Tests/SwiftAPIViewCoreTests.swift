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

import XCTest
@testable import SwiftAPIViewCore

class SwiftAPIViewCoreTests: XCTestCase {

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false
        SharedLogger.set(logger: NullLogger(), withLevel: .info)
    }

    private func pathFor(testFile filename: String) -> String {
        let bundle = Bundle(for: APIViewManager.self)
        if let filePath = bundle.path(forResource: filename, ofType: "swifttxt") {
            return filePath
        }
        XCTFail("Could not find file \(filename).swifttxt")
        return ""
    }

    private func contentsOf(expectFile filename: String) -> String {
        let bundle = Bundle(for: APIViewManager.self)
        let path = bundle.path(forResource: filename, ofType: "txt")!
        return try! String(contentsOfFile: path)
    }

    private func compare(expected: String, actual: String) {
        let actualLines = actual.split(separator: "\n", omittingEmptySubsequences: false).map { String($0) }
        let expectedLines = expected.split(separator: "\n", omittingEmptySubsequences: false).map { String($0) }
        for (i, expected) in expectedLines.enumerated() {
            let actual = actualLines[i]
            if (actual == expected) {
                continue
            }
            XCTFail("Line \(i): (\(actual) is not equal to (\(expected)")
        }
        XCTAssertEqual(actualLines.count, expectedLines.count, "Number of lines does not match")
    }

    func testAttributes() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "AttributesTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "AttributesExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testEnumerations() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "EnumerationsTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "EnumerationsExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testExtensions() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "ExtensionTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "ExtensionExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testFunctions() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "FunctionsTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "FunctionsExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testGenerics() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "GenericsTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "GenericsExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testInitializers() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "InitializersTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "InitializersExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testOperators() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "OperatorTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "OperatorExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testPrivateInternal() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "PrivateInternalTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "PrivateInternalExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testProperties() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "PropertiesTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "PropertiesExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testProtocols() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "ProtocolTestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "ProtocolExpectFile")
        compare(expected: expected, actual: generated)
    }

    func testSwiftUI() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = pathFor(testFile: "SwiftUITestFile")
        manager.config.packageVersion = "1.0.0"
        let generated = try manager.run()
        let expected = contentsOf(expectFile: "SwiftUIExpectFile")
        compare(expected: expected, actual: generated)
    }
}
