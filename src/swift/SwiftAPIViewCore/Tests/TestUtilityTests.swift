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

class TestUtilityTests: XCTestCase {

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false
        SharedLogger.set(logger: NullLogger(), withLevel: .info)
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

    func testReviewLineText() throws {
        let line = ReviewLine()
        var options = ReviewTokenOptions()
        options.hasSuffixSpace = false
        options.hasPrefixSpace = false
        line.tokens = [ReviewToken(kind: .text, value: "Some text", options: options)]
        let model = APIViewModel(packageName: "Test", packageVersion: "0.0", statements: [])
        model.reviewLines = [line, ReviewLine(), ReviewLine(), line]
        let generated = model.text
        let expected = "Some text\n\n\nSome text\n"
        compare(expected: expected, actual: generated)
    }

    func testReviewLineTextWithChildren() throws {
        let model = APIViewModel(packageName: "Test", packageVersion: "0.0", statements: [])
        var options = ReviewTokenOptions()
        options.hasSuffixSpace = false
        options.hasPrefixSpace = false
        let line1 = ReviewLine()
        line1.tokens = [ReviewToken(kind: .text, value: "Some text", options: options)]

        let line2 = ReviewLine()
        line2.tokens = [ReviewToken(kind: .text, value: "public class Foo()", options: options)]
        let child1 = ReviewLine()
        child1.tokens = [ReviewToken(kind: .text, value: "func getFoo() -> Foo", options: options)]
        let child2 = ReviewLine()
        let child3 = ReviewLine()
        child3.tokens = [ReviewToken(kind: .text, value: "func setFoo(_: Foo)", options: options)]
        line2.children = [child1, child2, child3]
        model.reviewLines = [line1, ReviewLine(), ReviewLine(), line2]
        let generated = model.text
        let expected = "Some text\n\n\npublic class Foo()\n    func getFoo() -> Foo\n\n    func setFoo(_: Foo)\n"
        compare(expected: expected, actual: generated)
    }

    func testSuffixSpaceBehavior() throws {
        let model = APIViewModel(packageName: "Test", packageVersion: "0.0", statements: [])
        let line = ReviewLine()
        var options = ReviewTokenOptions()
        options.hasPrefixSpace = false
        line.tokens = [ReviewToken(kind: .text, value: "A", options: options)]
        options.hasSuffixSpace = true
        line.tokens.append(ReviewToken(kind: .text, value: "B", options: options))
        options.hasSuffixSpace = false
        line.tokens.append(ReviewToken(kind: .text, value: "C", options: options))
        model.reviewLines = [line]
        let generated = model.text
        let expected = "A B C\n"
        compare(expected: expected, actual: generated)
    }

    func testPrefixSpaceBehavior() throws {
        let model = APIViewModel(packageName: "Test", packageVersion: "0.0", statements: [])
        let line1 = ReviewLine()
        var options = ReviewTokenOptions()
        options.hasSuffixSpace = false
        line1.tokens = [ReviewToken(kind: .text, value: "A", options: options)]
        options.hasPrefixSpace = true
        line1.tokens.append(ReviewToken(kind: .text, value: "B", options: options))
        options.hasPrefixSpace = false
        line1.tokens.append(ReviewToken(kind: .text, value: "C", options: options))

        let line2 = ReviewLine()
        options = ReviewTokenOptions()
        options.hasPrefixSpace = true
        options.hasSuffixSpace = nil
        line2.tokens = [ReviewToken(kind: .text, value: "A", options: options)]
        line2.tokens.append(ReviewToken(kind: .text, value: "B", options: options))
        line2.tokens.append(ReviewToken(kind: .text, value: "C", options: options))

        let line3 = ReviewLine()
        options = ReviewTokenOptions()
        options.hasPrefixSpace = true
        options.hasSuffixSpace = true
        line3.tokens = [ReviewToken(kind: .text, value: "A", options: options)]
        line3.tokens.append(ReviewToken(kind: .text, value: "B", options: options))
        line3.tokens.append(ReviewToken(kind: .text, value: "C", options: options))

        model.reviewLines = [line1, line2, line3]
        let generated = model.text
        let expected = "A BC\n A B C \n A B C \n"
        compare(expected: expected, actual: generated)
    }
}
