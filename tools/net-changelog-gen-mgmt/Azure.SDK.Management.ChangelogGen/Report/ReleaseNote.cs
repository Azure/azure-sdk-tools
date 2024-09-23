// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Compare;

namespace Azure.SDK.ChangelogGen.Report
{
    public class ReleaseNote
    {
        public string Prefix { get; set; }
        public string Note { get; set; }

        public ReleaseNote(string note, string prefix = "")
        {
            this.Note = note;
            this.Prefix = prefix;
        }

        public override string ToString()
        {
            return this.Prefix + this.Note;
        }

    }
}
