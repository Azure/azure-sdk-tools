// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.SDK.ChangelogGen.Compare
{
    public class ChangeSet
    {
        public List<Change> Changes { get; } = new List<Change>();

        public bool HasChange
        {
            get { return this.Changes.Count > 0; }
        }

        public List<Change> GetBreakingChanges()
        {
            return this.Changes.Where(t => t.IsBreakingChange).ToList();
        }

        public List<Change> GetNonBreakingChanges()
        {
            return this.Changes.Where(t => !t.IsBreakingChange).ToList();
        }
    }
}
