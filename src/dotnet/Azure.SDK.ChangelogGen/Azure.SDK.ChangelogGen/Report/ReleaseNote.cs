// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.SDK.ChangelogGen.Report
{
    public class ReleaseNote
    {
        public string Note { get; set; }

        public ReleaseNote(string note)
        {
            this.Note = note;
        }

        public override string ToString()
        {
            return this.Note;
        }
    }
}
