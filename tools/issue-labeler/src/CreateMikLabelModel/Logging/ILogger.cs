// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
