using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersMigration.Sections;
using Azure.Sdk.Tools.CodeownersMigration.Verification;

namespace Azure.Sdk.Tools.CodeownersMigration.Conversion
{
    public class ConversionOptions
    {
        public string CodeownersPath { get; set; }
        public string TeamStorageUri { get; set; }

        /// <summary>Sections whose entries are pushed out into owners.yaml fragments.</summary>
        public HashSet<string> FragmentSections { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Sections rendered with <c>protected: true</c>.</summary>
        public HashSet<string> ProtectedSections { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Glob describing where fragments live, e.g. <c>sdk/*/owners.yaml</c>.</summary>
        public string FragmentGlob { get; set; } = "sdk/*/owners.yaml";

        public string DefaultSection { get; set; }
        public string OutputPath { get; set; } = ".github/CODEOWNERS";
        public int MinimumPathOwners { get; set; } = 2;
        public int MinimumLabelOwners { get; set; } = 2;
    }

    public class ConversionResult
    {
        public string ConfigYaml { get; set; }

        /// <summary>Repo-relative fragment file path to YAML content.</summary>
        public Dictionary<string, string> Fragments { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Converts an existing CODEOWNERS file into an owners config plus owners.yaml fragments.
    /// </summary>
    public class CodeownersConverter
    {
        private readonly ConversionOptions _options;

        public CodeownersConverter(ConversionOptions options)
        {
            _options = options;
        }

        public ConversionResult Convert()
        {
            var result = new ConversionResult();
            CodeownersDocument document = CodeownersDocument.Load(_options.CodeownersPath, _options.TeamStorageUri);

            foreach (string diagnostic in document.ParseDiagnostics)
            {
                result.Warnings.Add($"Parser rejected a block and it will NOT be converted: {diagnostic}");
            }

            var fragmentPrefix = FragmentPrefix.Parse(_options.FragmentGlob);

            // Paths declared in sections that stay static. A fragment may not redefine any of these, so they
            // are collected before attribution.
            HashSet<string> staticPaths = CollectStaticPaths(document);

            var sectionModels = new List<SectionModel>();
            var fragmentModels = new Dictionary<string, FragmentModel>(StringComparer.Ordinal);

            foreach (CodeownersSection section in document.Sections)
            {
                var model = new SectionModel
                {
                    Name = section.Name,
                    IsProtected = _options.ProtectedSections.Contains(section.Name),
                    DefinedInFiles = _options.FragmentSections.Contains(section.Name)
                };
                sectionModels.Add(model);

                bool isFragmentSection = _options.FragmentSections.Contains(section.Name);
                List<EntryFacts> sectionFacts = document.FactsBySection.TryGetValue(section.Name, out List<EntryFacts> facts)
                    ? facts
                    : new List<EntryFacts>();

                foreach (EntryFacts entry in sectionFacts.Where(f => f.HasPath))
                {
                    string fragmentRoot = isFragmentSection ? fragmentPrefix.RootFor(entry.Path) : null;

                    // A fragment may not redefine a path that a static section already owns. The fragment
                    // section renders later, so the fragment entry would silently win under last-match-wins.
                    // Comparison is on the exact normalized path only; glob subset analysis is deliberately
                    // not attempted.
                    if (fragmentRoot != null && staticPaths.Contains(entry.Path))
                    {
                        result.Warnings.Add(
                            $"Path '{entry.Path}' in section '{section.Name}' is also defined statically in an " +
                            "earlier section. Left as a static entry; resolve the duplicate by hand before enabling the fragment.");
                        AddPathEntry(model.Paths, model.LabelOwners, entry, entry.DeclaredPath);
                        continue;
                    }

                    if (fragmentRoot == null)
                    {
                        if (isFragmentSection)
                        {
                            result.Warnings.Add(
                                $"Path '{entry.Path}' in section '{section.Name}' does not fit '{_options.FragmentGlob}'. Left as a static entry.");
                        }
                        AddPathEntry(model.Paths, model.LabelOwners, entry, entry.DeclaredPath);
                        continue;
                    }

                    FragmentModel fragment = GetOrCreateFragment(fragmentModels, fragmentRoot, section.Name);
                    AddPathEntry(fragment.Paths, fragment.LabelOwners, entry,
                                 fragmentPrefix.RelativePath(fragmentRoot, entry.DeclaredPath), SectionOverride(section.Name));
                }

                // Pathless blocks carry service label ownership only. They belong to no directory, so they
                // stay in the owners config with their section rather than being copied into fragments.
                foreach (EntryFacts entry in sectionFacts.Where(f => !f.HasPath))
                {
                    AddLabelOwners(model.LabelOwners, entry.ServiceLabels, entry.ServiceOwners, entry.AzureSdkOwners);
                }
            }

            result.ConfigYaml = RenderConfig(sectionModels);
            foreach (KeyValuePair<string, FragmentModel> pair in fragmentModels.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                // Paths keep the order they were declared in. CODEOWNERS is last-match-wins and the
                // renderer emits a fragment's paths in file order, so reordering here would silently
                // reassign ownership.
                string fragmentPath = pair.Key + "/" + fragmentPrefix.FileName;
                result.Fragments[fragmentPath] = RenderFragment(pair.Value);
            }

            return result;
        }

        private HashSet<string> CollectStaticPaths(CodeownersDocument document)
        {
            var staticPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (CodeownersSection section in document.Sections)
            {
                if (_options.FragmentSections.Contains(section.Name))
                {
                    continue;
                }
                if (!document.FactsBySection.TryGetValue(section.Name, out List<EntryFacts> facts))
                {
                    continue;
                }
                foreach (EntryFacts entry in facts.Where(f => f.HasPath))
                {
                    staticPaths.Add(entry.Path);
                }
            }
            return staticPaths;
        }

        /// <summary>
        /// Adds a path entry, splitting any service label ownership out into a label-owners entry. The new
        /// schema expresses service ownership only through label-owners, so a legacy block carrying both a
        /// path and a ServiceLabel becomes two declarations.
        /// </summary>
        /// <summary>The section to record on a fragment entry, or null when the default already routes it there.</summary>
        private string SectionOverride(string sectionName) =>
            string.IsNullOrEmpty(_options.DefaultSection) ||
            string.Equals(_options.DefaultSection, sectionName, StringComparison.OrdinalIgnoreCase)
                ? null
                : sectionName;

        private void AddPathEntry(List<PathModel> paths, List<LabelOwnerModel> labelOwners, EntryFacts entry, string path,
                                  string section = null)
        {
            paths.Add(new PathModel
            {
                Path = path,
                Owners = entry.SourceOwners,
                PRLabels = entry.PRLabels,
                Section = section
            });

            if (entry.ServiceLabels.Count > 0)
            {
                AddLabelOwners(labelOwners, entry.ServiceLabels, entry.ServiceOwners, entry.AzureSdkOwners, section);
            }
        }

        private static void AddLabelOwners(List<LabelOwnerModel> labelOwners,
                                           List<string> labels,
                                           List<string> serviceOwners,
                                           List<string> azureSdkOwners,
                                           string section = null)
        {
            if (labels == null || labels.Count == 0)
            {
                return;
            }

            string key = string.Join(",", EntryFacts.LabelSet(labels));
            LabelOwnerModel existing = labelOwners.FirstOrDefault(l => l.Key == key);
            if (existing == null)
            {
                existing = new LabelOwnerModel { Key = key, Labels = new List<string>(labels), Section = section };
                labelOwners.Add(existing);
            }

            MergeOwners(existing.ServiceOwners, serviceOwners);
            MergeOwners(existing.AzureSdkOwners, azureSdkOwners);
        }

        private static void MergeOwners(List<string> target, List<string> additions)
        {
            foreach (string owner in additions ?? new List<string>())
            {
                if (!target.Any(o => string.Equals(EntryFacts.OwnerKey(o), EntryFacts.OwnerKey(owner), StringComparison.Ordinal)))
                {
                    target.Add(owner);
                }
            }
        }

        private static FragmentModel GetOrCreateFragment(Dictionary<string, FragmentModel> fragments, string root, string sectionName)
        {
            if (!fragments.TryGetValue(root, out FragmentModel fragment))
            {
                fragment = new FragmentModel { Root = root, Section = sectionName };
                fragments[root] = fragment;
            }
            return fragment;
        }

        private string RenderConfig(List<SectionModel> sections)
        {
            var yaml = new YamlWriter();
            yaml.Comment("Generated by codeowners-migration convert.")
                .Comment("Review before committing: comments from the original CODEOWNERS are not carried over.")
                .Blank()
                .Scalar("version", 1)
                .Blank()
                .Key("configs")
                .Indent()
                .Key("allowed-owner-yaml-paths")
                .Indent();

            foreach (string glob in FragmentPrefix.AllowedGlobs(_options.FragmentGlob))
            {
                yaml.ItemScalarBare(glob);
            }

            yaml.Outdent();
            if (!string.IsNullOrEmpty(_options.DefaultSection))
            {
                yaml.Scalar("default-section", _options.DefaultSection);
            }
            yaml.Scalar("output", _options.OutputPath)
                .Scalar("minimum-path-owners", _options.MinimumPathOwners)
                .Scalar("minimum-label-owners", _options.MinimumLabelOwners)
                .Outdent()
                .Blank()
                .Key("sections")
                .Indent();

            foreach (SectionModel section in sections)
            {
                yaml.ItemScalar("name", section.Name);
                yaml.Indent();
                if (section.IsProtected)
                {
                    yaml.Scalar("protected", true);
                }
                if (section.DefinedInFiles)
                {
                    yaml.Scalar("defined-in-files", true);
                }
                WritePaths(yaml, section.Paths);
                WriteLabelOwners(yaml, section.LabelOwners);
                yaml.Outdent();
                yaml.Blank();
            }

            return yaml.ToString().TrimEnd('\n') + "\n";
        }

        private string RenderFragment(FragmentModel fragment)
        {
            var yaml = new YamlWriter();
            yaml.Comment("Generated by codeowners-migration convert.")
                .Comment("Paths are relative to this directory and may not escape it.")
                .Blank()
                .Scalar("version", 1);

            // A fragment has no file-level section: routing is declared per entry. A single fragment can hold
            // entries from more than one section, so each entry carries its own.
            WritePaths(yaml, fragment.Paths, leadingBlank: true);
            WriteLabelOwners(yaml, fragment.LabelOwners, leadingBlank: true);

            return yaml.ToString().TrimEnd('\n') + "\n";
        }

        private static void WritePaths(YamlWriter yaml, List<PathModel> paths, bool leadingBlank = false)
        {
            if (paths.Count == 0)
            {
                return;
            }

            if (leadingBlank)
            {
                yaml.Blank();
            }
            yaml.Key("paths").Indent();
            foreach (PathModel path in paths)
            {
                yaml.ItemScalar("path", path.Path);
                yaml.Indent();
                yaml.FlowSequence("owners", path.Owners);
                yaml.FlowSequence("pr-labels", path.PRLabels);
                if (path.Section != null)
                {
                    yaml.Scalar("section", path.Section);
                }
                yaml.Outdent();
            }
            yaml.Outdent();
        }

        private static void WriteLabelOwners(YamlWriter yaml, List<LabelOwnerModel> labelOwners, bool leadingBlank = false)
        {
            if (labelOwners.Count == 0)
            {
                return;
            }

            if (leadingBlank)
            {
                yaml.Blank();
            }
            yaml.Key("label-owners").Indent();
            foreach (LabelOwnerModel labelOwner in labelOwners)
            {
                yaml.ItemFlowSequence("labels", labelOwner.Labels);
                yaml.Indent();
                yaml.FlowSequence("service-owners", labelOwner.ServiceOwners);
                yaml.FlowSequence("azure-sdk-owners", labelOwner.AzureSdkOwners);
                if (labelOwner.Section != null)
                {
                    yaml.Scalar("section", labelOwner.Section);
                }
                yaml.Outdent();
            }
            yaml.Outdent();
        }

        private class SectionModel
        {
            public string Name { get; set; }
            public bool IsProtected { get; set; }
            public bool DefinedInFiles { get; set; }
            public List<PathModel> Paths { get; } = new List<PathModel>();
            public List<LabelOwnerModel> LabelOwners { get; } = new List<LabelOwnerModel>();
        }

        private class FragmentModel
        {
            public string Root { get; set; }
            public string Section { get; set; }
            public List<PathModel> Paths { get; } = new List<PathModel>();
            public List<LabelOwnerModel> LabelOwners { get; } = new List<LabelOwnerModel>();
        }

        private class PathModel
        {
            public string Path { get; set; }
            public List<string> Owners { get; set; } = new List<string>();
            public List<string> PRLabels { get; set; } = new List<string>();

            /// <summary>Section this entry was declared under. Null in the owners config, where the
            /// section already provides the context.</summary>
            public string Section { get; set; }
        }

        private class LabelOwnerModel
        {
            public string Key { get; set; }
            public List<string> Labels { get; set; } = new List<string>();
            public List<string> ServiceOwners { get; } = new List<string>();
            public List<string> AzureSdkOwners { get; } = new List<string>();
            public string Section { get; set; }
        }
    }
}
