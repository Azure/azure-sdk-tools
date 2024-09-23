// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Markdig.Syntax;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.SDK.ChangelogGen.Utilities
{
    internal static class Helper
    {
        #region release
        public static string GetLastReleaseVersionFromGitBranch(this Branch branch, string fileKey, bool includePreview, out string releaseDate)
        {
            string content = branch.GetFileContent(fileKey);
            return GetLastReleaseVersion(content, includePreview, out releaseDate);
        }

        public static string GetLastReleaseVersionFromGitTree(this Tree tree, string fileKey, bool includePreview, out string releaseDate)
        {
            string content = tree.GetFileContent(fileKey);
            return GetLastReleaseVersion(content, includePreview, out releaseDate);
        }

        public static string GetLastReleaseVersionFromFile(string path, bool includePreview, out string releaseDate)
        {
            string content = File.ReadAllText(path);
            return GetLastReleaseVersion(content, includePreview, out releaseDate);
        }

        private static string GetLastReleaseVersion(string changelogContent, bool includePreview, out string releaseDate)
        {
            Regex ver = new Regex(@"^##\s+(?<ver>[\w\.\-]+?)\s+\((?<date>\d{4}-\d{2}-\d{2})\)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            var matches = ver.Matches(changelogContent).Where(m => includePreview || !IsPreviewRelease(m.Groups["ver"].Value)).ToList();
            if (matches.Count == 0)
            {
                releaseDate = "";
                return "";
            }
            else
            {
                var firstMatch = matches[0];
                releaseDate = firstMatch.Groups["date"].Value;
                return firstMatch.Groups["ver"].Value;
            }
        }

        public static bool IsPreviewRelease(string version)
        {
            return Regex.IsMatch(version, @"beta|alpha|preview", RegexOptions.IgnoreCase);
        }
        #endregion

        #region git
        public static string GetFileContent(this TreeEntry te)
        {
            Debug.Assert(te.TargetType == TreeEntryTargetType.Blob);
            var blob = (LibGit2Sharp.Blob)te.Target;

            var contentStream = blob.GetContentStream();

            using (var tr = new StreamReader(contentStream, Encoding.UTF8))
            {
                return tr.ReadToEnd();
            }
        }

        public static Branch GetBranch(this Repository repo, string? branchFriendlyName = null)
        {
            var b = string.IsNullOrEmpty(branchFriendlyName) ? repo.Branches.FirstOrDefault(b => b.IsCurrentRepositoryHead) : repo.Branches.FirstOrDefault(b => b.FriendlyName == branchFriendlyName);
            if (b == null)
            {
                throw new InvalidOperationException("Can't find branch with FriendlyName " + (branchFriendlyName ?? "CurrentBranch"));
            }
            return b;
        }

        public static Tree GetTreeByTag(this Repository repo, string tagFriendlyName)
        {
            var tag = repo.Tags.FirstOrDefault(t => t.FriendlyName == tagFriendlyName) ??
                throw new InvalidOperationException($"Can't find Tag with FriendlyName {tagFriendlyName}. Please make sure the Tag exists (i.e. not released yet?) and has been refreshed properly locally (i.e. by 'git fetch upstream main --tags')");
            var commit = repo.Lookup<Commit>(tag.Target.Id) ??
                throw new InvalidOperationException("Can't find the commit for Tag " + tagFriendlyName);
            return commit.Tree;
        }

        public static string GetFileContent(this Branch branch, string fileKey)
        {
            return branch.Tip.Tree.GetFileContent(fileKey);
        }

        public static string GetFileContent(this Tree tree, string fileKey)
        {
            string[] segs = fileKey.Split('\\', '/');

            if (segs.Length == 0)
                throw new ArgumentNullException(nameof(fileKey));

            Tree cur = tree;
            for (int i = 0; i < segs.Length - 1; i++)
            {
                TreeEntry? curSeg = cur.FirstOrDefault(c => c.Name.ToLower() == segs[i].ToLower()) ??
                    throw new InvalidOperationException($"Can't find file '{fileKey}' in Git at '{segs[i]}'. Git tree id = '{tree.Id}'");
                Debug.Assert(curSeg.Target.GetType() == typeof(Tree));
                cur = (Tree)curSeg.Target;
            }
            var te = cur.FirstOrDefault(c => c.Name.ToLower() == segs[^1].ToLower()) ??
                throw new InvalidOperationException($"Cannot find file '{fileKey}' in Git at '{segs[^1]}'. Git tree id = '{tree.Id}'");

            return te.GetFileContent();
        }
        #endregion

        #region api
        public static bool IsObsoleted(this PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<ObsoleteAttribute>() != null;
        }

        public static bool IsObsoleted(this MethodInfo methodInfo)
        {
            return methodInfo.GetCustomAttribute<ObsoleteAttribute>() != null;
        }

        public static bool IsObsoleted(this Type type)
        {
            return type.GetCustomAttribute<ObsoleteAttribute>() != null;
        }

        public static bool IsObsoleted(this ConstructorInfo constructorInfo)
        {
            return constructorInfo.GetCustomAttribute<ObsoleteAttribute>() != null;
        }

        public static MethodInfo[] GetMethods(this Type type, BindingFlags flags, bool includePropertyMethod)
        {
            if (includePropertyMethod)
                return type.GetMethods(flags);
            else
                return type.GetMethods(flags).Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")).ToArray();
        }

        public static string ToFriendlyString(this PropertyInfo pi)
        {
            if (pi == null)
                throw new ArgumentNullException(nameof(pi));

            var indexParameters = pi.GetIndexParameters();
            if (indexParameters.Length > 0)
            {
                return $"{pi.PropertyType.ToFriendlyString()} {pi.DeclaringType!.ToFriendlyString()}[{string.Join(", ", indexParameters.Select(p => p.ParameterType.ToFriendlyString()))}]";
            }
            else
            {
                return $"{pi.PropertyType.ToFriendlyString()} {pi.Name}";
            }
        }

        public static string ToFriendlyString(this MethodInfo mi)
        {
            if (mi == null)
                throw new ArgumentNullException(nameof(mi));

            var genericArguments = mi.GetGenericArguments();
            var genericPart = genericArguments.Length == 0 ? "" : $"<{string.Join(", ", genericArguments.Select(g => g.Name))}>";
            var paramList = mi.GetParameters();
            var returnType = mi.ReturnType;

            string paramString = string.Join(", ", paramList.Select(p => $"{p.ParameterType.ToFriendlyString()} {p.Name}{(p.HasDefaultValue && p.DefaultValue != null ? $" = {p.DefaultValue}" : "")}"));
            return $"{returnType.ToFriendlyString()} {mi.Name}{genericPart}({paramString})";
        }

        public static string ToFriendlyString(this Type type, bool fullName = false)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var gtp = type.GetGenericArguments();
            var genericPart = gtp.Length == 0 ? "" : $"<{string.Join(", ", gtp.Select(t => t.ToFriendlyString(fullName)))}>";
            string name = (fullName && type.FullName != null) ? type.FullName : type.Name;
            int index = name.IndexOf('`');
            if (index >= 0)
                name = name[..index];
            return $"{name}{genericPart}";
        }

        public static string ToFriendlyString(this ConstructorInfo ci)
        {
            if (ci == null)
                throw new ArgumentNullException(nameof(ci));

            var paramList = ci.GetParameters();

            return $"{ci.Name}({string.Join(", ", paramList.Select(p => $"{p.ParameterType.ToFriendlyString()} {p.Name}"))})";
        }

        public static string GetKey(this Type type)
        {
            return type.FullName!;
        }

        public static string GetKey(this MethodInfo mi)
        {
            // Use Friendly string we parsed instead of default ToString() because the default ToString() won't consider the parameter default value
            return mi.ToFriendlyString();
        }

        public static string GetKey(this PropertyInfo pi)
        {
            return pi.ToString()!;
        }

        public static string GetKey(this ConstructorInfo pi)
        {
            return pi.ToString()!;
        }
        #endregion

        #region markdown
        public static Dictionary<string, object> LoadYaml(this MarkdownDocument md)
        {
            var lines = md.Where(b =>
            {
                FencedCodeBlock? fcb = b as FencedCodeBlock;
                if (fcb == null)
                    return false;
                return (fcb.Info == "yaml" && string.IsNullOrEmpty(fcb.Arguments));
            }).SelectMany(b => ((FencedCodeBlock)b).Lines.Lines).ToList();

            // TODO: add support to include yaml from conditional block in markdown when needed
            var allYaml = string.Join("\n", lines);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .Build();
            return deserializer.Deserialize<Dictionary<string, object>>(allYaml);
        }
        #endregion
    }
}
