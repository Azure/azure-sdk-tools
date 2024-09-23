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

public enum LogLevel: Int {
    case error, warning, info, debug

    var label: String {
        switch self {
        case .error:
            return "ERROR"
        case .warning:
            return "WARN"
        case .info:
            return "INFO"
        case .debug:
            return "DEBUG"
        }
    }
}

public protocol Logger {
    var level: LogLevel { get set }

    func log(_ message: @autoclosure @escaping () -> String?, category: String?, level: LogLevel)
    func fail(_ message: @autoclosure @escaping () -> String?, category: String?) -> Never
}

extension Logger {
    func shouldLog(forLevel level: LogLevel) -> Bool {
        return level.rawValue <= self.level.rawValue
    }
}

// MARK: - Implementation

public struct SharedLogger {
    private static var logger: Logger!

    static var level: LogLevel {
        set {
            logger.level = newValue
        }

        get {
            logger.level
        }
    }

    public static let `default` = "default"

    public static func set(logger: Logger, withLevel level: LogLevel = .info) {
        SharedLogger.logger = logger
        SharedLogger.level = level
    }

    public static func error(
        _ message: @autoclosure @escaping () -> String?,
        category: String? = nil
    ) {
        log(message(), category: category, level: .error)
    }

    public static func warn(
        _ message: @autoclosure @escaping () -> String?,
        category: String? = nil
    ) {
        log(message(), category: category, level: .warning)
    }

    public static func info(
        _ message: @autoclosure @escaping () -> String?,
        category: String? = nil
    ) {
        log(message(), category: category, level: .info)
    }

    public static func debug(
        _ message: @autoclosure @escaping () -> String?,
        category: String? = nil
    ) {
        log(message(), category: category, level: .debug)
    }

    private static func log(
        _ message: @autoclosure @escaping () -> String?,
        category: String? = nil,
        level: LogLevel
    ) {
        guard logger.shouldLog(forLevel: level) else { return }
        SharedLogger.logger.log(message(), category: category ?? SharedLogger.default, level: level)
    }

    public static func fail(
        _ message: @autoclosure @escaping () -> String?,
        category: String = SharedLogger.default
    ) -> Never {
        SharedLogger.logger.fail(message(), category: category)
    }
}

/// Do-nothing logger
public class NullLogger: Logger {
    public var level: LogLevel = .info

    public func log(_: @autoclosure @escaping () -> String?, category _: String? = nil, level _: LogLevel = .info) {}

    public func fail(_: @autoclosure @escaping () -> String?, category _: String? = nil) -> Never {
        fatalError()
    }
}

/// Stdiout logger
public class StdoutLogger: Logger {
    // MARK: Properties

    public var level: LogLevel

    // MARK: Initializer

    public init(logLevel: LogLevel = .info) {
        level = logLevel
    }

    // MARK: Methods

    public func log(_ message: @autoclosure @escaping () -> String?, category: String? = nil, level: LogLevel) {
        guard let msg = message() else {
            fatalError("Unable to create log message.")
        }
        guard shouldLog(forLevel: level) else { return }
        let cat = category ?? SharedLogger.default
        print("SwiftAPIView.\(cat) (\(level.label)) \(msg)")
    }

    public func fail(_ message: @autoclosure @escaping () -> String?, category: String? = nil) -> Never {
        guard let msg = message() else {
            fatalError("Unable to create log message.")
        }
        log(msg, category: category, level: .error)
        fatalError(msg)
    }
}
