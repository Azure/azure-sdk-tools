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
        SharedLogger.set(logger: NullLogger(), withLevel: .info)
    }

    private func load(testFile filename: String) -> String {
        let bundle = Bundle(for: Swift.type(of: self))
        return bundle.path(forResource: filename, ofType: "swifttxt")!
    }

    private func load(expectFile filename: String) -> String {
        let bundle = Bundle(for: Swift.type(of: self))
        let path = bundle.path(forResource: filename, ofType: "txt")!
        return try! String(contentsOfFile: path)
    }

    private func compare(expected: String, actual: String) {
        let actualLines = actual.split(separator: "\n").map { String($0) }
        let expectedLines = expected.split(separator: "\n").map { String($0) }
        print(actual)
        XCTAssertEqual(actualLines.count, expectedLines.count)
        for (i, expected) in expectedLines.enumerated() {
            let actual = actualLines[i]
            XCTAssert(actual == expected, "Line \(i): (\(actual) is not equal to (\(expected)")
        }
    }

    func testFile1() throws {
        let manager = APIViewManager(mode: .testing)
        manager.config.sourcePath = load(testFile: "TestFile1")
        manager.config.packageVersion = "1.0.0"
        let generated = try! manager.run()
        let expected = load(expectFile: "ExpectFile1")
        compare(expected: expected, actual: generated)
    }
}
