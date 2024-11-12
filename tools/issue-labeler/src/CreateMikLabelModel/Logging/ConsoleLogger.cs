// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace CreateMikLabelModel
{
    /// <summary>
    ///   Logs information to the <see cref="Console" /> using the
    ///   standard streams.
    /// </summary>
    ///
    public class ConsoleLogger : ILogger
    {
        /// <summary>
        ///   Logs an informational message.
        /// </summary>
        ///
        /// <param name="message">The message to log.</param>
        ///
        public void LogInformation(string message) => Console.WriteLine(message);

        /// <summary>
        ///   Logs a warning message.
        /// </summary>
        ///
        /// <param name="message">The message to log.</param>
        ///
        public void LogWarning(string message)
        {
            var color = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ForegroundColor = color;
        }
    }
}
