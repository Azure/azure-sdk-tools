// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// This file demonstrates the correct implementation of a model factory for the Azure.Template project
// to resolve the AZC0035 analyzer warning for SecretBundle.

using System.Collections.Generic;

namespace Azure.Template
{
    /// <summary>
    /// Model factory that creates models for mocking scenarios in Azure.Template.
    /// </summary>
    public static class TemplateModelFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="Models.SecretBundle"/> for mocking purposes.
        /// </summary>
        /// <param name="value">The secret value.</param>
        /// <param name="id">The secret id.</param>
        /// <param name="contentType">The content type of the secret.</param>
        /// <param name="tags">Application specific metadata in the form of key-value pairs.</param>
        /// <param name="kid">If this is a secret backing a KV certificate, then this field specifies the corresponding key backing the KV certificate.</param>
        /// <param name="managed">True if the secret's lifetime is managed by key vault. If this is a secret backing a certificate, then managed will be true.</param>
        /// <returns>A new <see cref="Models.SecretBundle"/> instance for mocking.</returns>
        public static Models.SecretBundle SecretBundle(
            string value = null,
            string id = null,
            string contentType = null,
            IReadOnlyDictionary<string, string> tags = null,
            string kid = null,
            bool? managed = null)
        {
            // In a real implementation, this would use internal constructors or reflection
            // to create the model instance with the provided parameters.
            // For demonstration purposes, we return null here.
            return new Models.SecretBundle(value, id, contentType, tags, kid, managed);
        }
    }
}

namespace Azure.Template.Models
{
    // This is a simplified version of SecretBundle for demonstration
    // The actual implementation would be in the Azure.Template project
    public partial class SecretBundle
    {
        internal SecretBundle(string value, string id, string contentType, IReadOnlyDictionary<string, string> tags, string kid, bool? managed)
        {
            Value = value;
            Id = id;
            ContentType = contentType;
            Tags = tags ?? new Dictionary<string, string>();
            Kid = kid;
            Managed = managed;
        }

        public string Value { get; }
        public string Id { get; }
        public string ContentType { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public string Kid { get; }
        public bool? Managed { get; }
    }
}