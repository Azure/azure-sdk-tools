// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Notification
{
    /// <summary>
    /// Base class describing the payload sent to the notification service. Concrete templates
    /// inherit this type and construct the <see cref="Subject"/> and <see cref="Body"/> from their
    /// own data. Recipients are set by the notification service before sending.
    /// </summary>
    public abstract class EmailPayload
    {
        /// <summary>
        /// Primary recipients of the email.
        /// </summary>
        public List<string> EmailTo { get; set; } = [];

        /// <summary>
        /// Carbon copy recipients of the email.
        /// </summary>
        public List<string> CC { get; set; } = [];

        /// <summary>
        /// The email subject.
        /// </summary>
        public abstract string Subject { get; }

        /// <summary>
        /// The email body.
        /// </summary>
        public abstract string Body { get; }
    }
}
