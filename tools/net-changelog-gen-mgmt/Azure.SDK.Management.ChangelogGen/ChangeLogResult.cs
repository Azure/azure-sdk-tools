// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen
{
    public class ChangeLogResult
    {
        public ChangeSet? ApiChange { get; set; } = null;
        public StringValueChange? SpecVersionChange { get; set; } = null;
        public StringValueChange? AzureCoreVersionChange { get; set; } = null;
        public StringValueChange? AzureResourceManagerVersionChange { get; set; } = null;

        public Release GenerateReleaseNote(string version, string date, List<ChangeCatogory> filter)
        {
            const string PREFIX = "- ";
            Release report = new Release(version, date);

            if (ApiChange!= null && ApiChange.GetBreakingChanges().Any())
            {
                ReleaseNoteGroup breakingGroup = new ReleaseNoteGroup("Breaking Changes");
                var breaking = ApiChange?.GetBreakingChanges();
                if (breaking != null && breaking.Count > 0)
                {
                    breakingGroup.Notes.AddRange(breaking.OrderBy(b => $"{b.ChangeCatogory}/{b.Target}").Select(b => new ReleaseNote(b.Description, PREFIX)));
                }
                report.Groups.Add(breakingGroup);
                Logger.Error("Breaking change detected which is not expected\n" + breakingGroup.ToString());
            }

            ReleaseNoteGroup featureAddedGroup = new ReleaseNoteGroup("Features Added");
            if (SpecVersionChange != null)
                featureAddedGroup.Notes.Add(new ReleaseNote(SpecVersionChange.Description, PREFIX));
            if (featureAddedGroup.Notes.Count > 0)
                report.Groups.Add(featureAddedGroup);

            ReleaseNoteGroup othersGroup = new ReleaseNoteGroup("Other Changes");
            if (AzureCoreVersionChange != null)
                othersGroup.Notes.Add(new ReleaseNote(AzureCoreVersionChange.Description, PREFIX));
            if (AzureResourceManagerVersionChange != null)
                othersGroup.Notes.Add(new ReleaseNote(AzureResourceManagerVersionChange.Description, PREFIX));

            var nonbreaking = ApiChange?.GetNonBreakingChanges().Where(b => filter.Count == 0 || filter.Contains(b.ChangeCatogory)).ToList();
            if (nonbreaking != null && nonbreaking.Count > 0)
            {
                othersGroup.Notes.AddRange(nonbreaking.OrderBy(b => $"{b.ChangeCatogory}/{b.Target}").Select(b => new ReleaseNote(b.Description, PREFIX)));
            }
            if(othersGroup.Notes.Count > 0)
                report.Groups.Add(othersGroup);

            return report;
        }
    }
}
