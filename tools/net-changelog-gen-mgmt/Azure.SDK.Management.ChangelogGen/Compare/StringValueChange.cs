// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.SDK.ChangelogGen.Compare
{
    public class StringValueChange : Change
    {
        public string NewValue { get; init; }
        public string OldValue { get; init; }

        public override string Description { get; }

        public StringValueChange(string newValue, string oldValue, string description, ChangeCatogory category = ChangeCatogory.Updated)
            : base(ChangeTarget.StringValue, category)
        {
            this.NewValue = newValue;
            this.OldValue = oldValue;
            this.Description = description;
        }
    }
}
