// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.SDK.ChangelogGen.Compare
{
    public enum ChangeCatogory
    {
        Added,
        Removed,
        Obsoleted,
        Updated,
    }

    public enum ChangeTarget
    {
        Type,
        Constructor,
        Method,
        Property,
        PropertyMethod,
        StringValue,
    }

    public abstract class Change
    {
        public ChangeCatogory ChangeCatogory { get; init; }
        public ChangeTarget Target { get; init; }

        public Change(ChangeTarget target, ChangeCatogory category)
        {
            this.ChangeCatogory = category;
            this.Target = target;
        }

        public abstract string Description { get; }
        public virtual bool IsBreakingChange
        {
            get { return this.ChangeCatogory == ChangeCatogory.Removed; }
        }
    }
}
