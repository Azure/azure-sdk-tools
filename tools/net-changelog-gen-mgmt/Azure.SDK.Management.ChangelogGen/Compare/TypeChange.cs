// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Compare
{
    public class TypeChange : Change
    {
        public Type Type { get; init; }

        public override string Description
        {
            get
            {
                return this.ChangeCatogory switch
                {
                    ChangeCatogory.Added => $"Added type '{this.Type.ToFriendlyString(fullName: true)}'",
                    ChangeCatogory.Removed => $"Removed type '{this.Type.ToFriendlyString(fullName: true)}'",
                    ChangeCatogory.Obsoleted => $"Obsoleted type '{this.Type.ToFriendlyString(fullName: true)}'",
                    _ => throw new InvalidOperationException("Unhandled ChangeCategory for Type")
                };
            }
        }

        public TypeChange(Type type, ChangeCatogory category)
            : base(ChangeTarget.Type, category)
        {
            this.Type = type;
        }
    }
}
