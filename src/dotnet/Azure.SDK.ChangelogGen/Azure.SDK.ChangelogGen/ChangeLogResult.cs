// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;

namespace Azure.SDK.ChangelogGen
{
    public class ChangeLogResult
    {
        public ChangeSet? ApiChange { get; set; } = null;
        public StringValueChange? SpecVersionChange { get; set; } = null;
        public StringValueChange? AzureCoreVersionChange { get; set; } = null;
        public StringValueChange? AzureResourceManagerVersionChange { get; set; } = null;

        public Release GenerateReleaseNote()
        {
            Release report = new Release("release_version", "release_date");

            ReleaseNoteGroup breakingGroup = new ReleaseNoteGroup("Breaking Changes in API");
            var breaking = ApiChange?.GetBreakingChanges();
            if (breaking != null && breaking.Count > 0)
            {
                breakingGroup.Notes.AddRange(breaking.OrderBy(b => $"{b.ChangeCatogory}/{b.Target}").Select(b => new ReleaseNote(b.Description)));
            }
            report.Groups.Add(breakingGroup);


            ReleaseNoteGroup nonbreakingGroup = new ReleaseNoteGroup("Other Changes in API");
            var nonbreaking = ApiChange?.GetNonBreakingChanges();
            if (nonbreaking != null && nonbreaking.Count > 0)
            {
                nonbreakingGroup.Notes.AddRange(nonbreaking.OrderBy(b => $"{b.ChangeCatogory}/{b.Target}").Select(b => new ReleaseNote(b.Description)));
            }
            report.Groups.Add(nonbreakingGroup);

            // API versoin
            ReleaseNoteGroup apiVersionGroup = new ReleaseNoteGroup("Api Version Change");
            if (SpecVersionChange != null)
            {
                apiVersionGroup.Notes.Add(new ReleaseNote(SpecVersionChange.Description));
            }
            report.Groups.Add(apiVersionGroup);

            // Azure Core
            ReleaseNoteGroup depGroup = new ReleaseNoteGroup("Azure SDK Dependency Changes");
            if (AzureCoreVersionChange != null || AzureResourceManagerVersionChange != null)
            {
                if (AzureCoreVersionChange != null)
                    depGroup.Notes.Add(new ReleaseNote(AzureCoreVersionChange.Description));
                if (AzureResourceManagerVersionChange != null)
                    depGroup.Notes.Add(new ReleaseNote(AzureResourceManagerVersionChange.Description));
            }
            report.Groups.Add(depGroup);

            return report;
        }
    }
}
