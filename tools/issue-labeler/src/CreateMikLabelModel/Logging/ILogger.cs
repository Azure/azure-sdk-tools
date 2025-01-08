// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CreateMikLabelModel
{
    /// <summary>
    ///   Allows messages of different categories to be logged.
    /// </summary>
    ///
    public interface ILogger
    {
        /// <summary>
        ///   Logs an informational message.
        /// </summary>
        ///
        /// <param name="message">The message to log.</param>
        ///
        void LogInformation(string message);

        /// <summary>
        ///   Logs a warning message.
        /// </summary>
        ///
        /// <param name="message">The message to log.</param>
        ///
        void LogWarning(string message);
    }
}
