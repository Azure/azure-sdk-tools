// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ApiView;
using APIView.DIff;
using APIView.Model;

namespace APIViewWeb.Models
{
    public enum RenderType { Normal, ReadOnly, Text }

    public class RenderedCodeFile
    {
        private CodeLine[] _rendered;
        private CodeLine[] _renderedReadOnly;
        private CodeLine[] _renderedText;

        public RenderedCodeFile(CodeFile codeFile)
        {
            CodeFile = codeFile;
        }

        public CodeFile CodeFile { get; }
        public RenderResult RenderResult { get; private set; }

        public RenderResult RenderResultReadOnly { get; private set; }

        public RenderResult RenderResultText { get; private set; }

        public CodeLine[] Render(bool showDocumentation)
        {
            //Always render when documentation is requested to avoid cach thrashing
            if (showDocumentation)
            {
                RenderResult = CodeFileHtmlRenderer.Normal.Render(CodeFile, showDocumentation: true);
                return RenderResult.CodeLines;
            }

            if (_rendered == null)
            {
                RenderResult = CodeFileHtmlRenderer.Normal.Render(CodeFile);
                _rendered = RenderResult.CodeLines;
            }

            return _rendered;
        }

        public CodeLine[] RenderReadOnly(bool showDocumentation)
        {
            if (showDocumentation)
            {
                RenderResultReadOnly = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile, showDocumentation: true);
                return RenderResultReadOnly.CodeLines;
            }

            if (_renderedReadOnly == null)
            {
                RenderResultReadOnly = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile);
                _renderedReadOnly = RenderResultReadOnly.CodeLines;
            }

            return _renderedReadOnly;
        }

        internal CodeLine[] RenderText(bool showDocumentation, bool skipDiff = false)
        {
            if (showDocumentation || skipDiff)
            {
                RenderResultText = CodeFileRenderer.Instance.Render(CodeFile, showDocumentation: showDocumentation, enableSkipDiff: skipDiff);
                return RenderResultText.CodeLines;
            }

            if (_renderedText == null)
            {
                RenderResultText = CodeFileRenderer.Instance.Render(CodeFile);
                _renderedText = RenderResultText.CodeLines;
            }

            return _renderedText;
        }
        /// <summary>
        /// Get CodeLines from Section in RenderResult corresponding to the passed SectionId
        /// Add classes for managing hierachy of CodeLines
        /// </summary>
        /// <param name="sectionId">key for section in RenderResult.Sections</param>
        /// <param name="renderType"></param>
        /// <param name="showDocumentation"></param>
        /// <param name="skipDiff"></param>
        /// <returns></returns>
        public CodeLine[] GetCodeLineSection(int sectionId = 0, RenderType renderType = RenderType.Normal, bool showDocumentation = false, bool skipDiff = false)
        {
            var result = new List<CodeLine>();
            RenderResult renderResult = GetRenderResult(renderType, showDocumentation, skipDiff);

            if (renderResult.Sections.Count > sectionId)
            {
                var section = renderResult.Sections[sectionId];

                using (IEnumerator<TreeNode<CodeLine>> enumerator = section.GetEnumerator())
                {
                    enumerator.MoveNext();
                    while (enumerator.MoveNext())
                    {
                        var node = enumerator.Current;
                        var lineClass = new List<string>();
                        var indent = node.Level;

                        // Add classes for managing tree hierachy
                        if (node.Children.Count > 0)
                            lineClass.Add($"lvl_{node.Level}_parent_{node.PositionAmongSiblings}");

                        if (!node.IsRoot)
                            lineClass.Add($"lvl_{node.Level}_child_{node.PositionAmongSiblings}");

                        if (node.Level > 1)
                            lineClass.Add("d-none");

                        var lineClasses = String.Join(' ', lineClass);

                        if (!String.IsNullOrWhiteSpace(node.Data.LineClass))
                            lineClasses = node.Data.LineClass.Trim() + $" {lineClasses}";

                        if (node.IsLeaf)
                        {
                            CodeLine[] renderedLeafSection = GetDetachedLeafSectionLines(node, renderType: renderType, skipDiff: skipDiff);

                            if (renderedLeafSection.Length > 0)
                            {
                                var placeHolderLineNumber = node.Data.LineNumber;
                                int index = 0;
                                foreach (var codeLine in renderedLeafSection)
                                {
                                    index++;
                                    lineClasses = Regex.Replace(lineClasses, @"_child_[0-9]+", $"_child_{index}");
                                    if (!String.IsNullOrWhiteSpace(codeLine.LineClass))
                                    {
                                        lineClasses = codeLine.LineClass.Trim() + $" {lineClasses}";
                                    }
                                    result.Add(new CodeLine(codeLine, lineClass: lineClasses, lineNumber: placeHolderLineNumber, indent: indent));
                                    placeHolderLineNumber++;
                                }
                            }
                            else
                            {
                                result.Add(new CodeLine(node.Data, lineClass: lineClasses, indent: indent));
                            }
                        }
                        else
                        {
                            result.Add(new CodeLine(node.Data, lineClass: lineClasses, indent: indent));
                        }
                    }
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Compute diff CodeLines from rootNode of InlineDiffLine
        /// </summary>
        public InlineDiffLine<CodeLine>[] GetDiffCodeLineSection(TreeNode<InlineDiffLine<CodeLine>> sectionNode, bool skipDiff = false)
        {
            var diffLines = new List<InlineDiffLine<CodeLine>>();

            using (IEnumerator<TreeNode<InlineDiffLine<CodeLine>>> enumerator = sectionNode.GetEnumerator())
            {
                TreeNode<InlineDiffLine<CodeLine>> detachedLeafParent = null;
                InlineDiffLine<CodeLine> diffLine;
                int ? detachedLeafParentLineNo = null;
                var childNodeHasDiff = false;

                enumerator.MoveNext();
                while (enumerator.MoveNext())
                {
                    var node = enumerator.Current;
                    if (node.WasDetachedLeafParent)
                    {
                        detachedLeafParent = node;
                        detachedLeafParentLineNo = detachedLeafParent.Data.Line.LineNumber;
                        continue;
                    }

                    if (!node.IsLeaf)
                    {
                        detachedLeafParent = null;
                        detachedLeafParentLineNo = null;
                    }

                    var lineClass = new List<string>();
                    var level = (detachedLeafParent == null) ? node.Level : node.Level - 1;

                    // Add classes for managing tree hierachy
                    if (node.Children.Count > 0)
                        lineClass.Add($"lvl_{level}_parent_{node.PositionAmongSiblings}");

                    if (!node.IsRoot)
                        lineClass.Add($"lvl_{level}_child_{node.PositionAmongSiblings}");

                    if (level > 1)
                        lineClass.Add("d-none");

                    var lineClasses = String.Join(' ', lineClass);

                    if (!String.IsNullOrWhiteSpace(node.Data.Line.LineClass))
                        lineClasses = node.Data.Line.LineClass.Trim() + $" {lineClasses}";

                    if (detachedLeafParent != null && detachedLeafParent.IsParentOf(node))
                    {
                        childNodeHasDiff = ChildNodeHasDiff(node);
                        var codeLine = new CodeLine(node.Data.Line, lineClass: lineClasses, lineNumber: detachedLeafParentLineNo, indent: level);
                        diffLine = new InlineDiffLine<CodeLine>(codeLine, node.Data.Kind, childNodeHasDiff);
                        diffLines.Add(diffLine);
                        detachedLeafParentLineNo++;
                    }
                    else
                    {
                        childNodeHasDiff = ChildNodeHasDiff(node);
                        var codeLine = new CodeLine(node.Data.Line, lineClass: lineClasses, indent: level);
                        diffLine = new InlineDiffLine<CodeLine>(codeLine, node.Data.Kind, childNodeHasDiff);
                        diffLines.Add(diffLine);
                    }
                }
            }
            return diffLines.ToArray();
        }

        /// <summary>
        /// Determine if any descendant of the node is a diff change.
        /// </summary>
        public bool ChildNodeHasDiff (TreeNode<InlineDiffLine<CodeLine>> sectionNode)
        {
            bool childNodeHasDiff = false;
            using (IEnumerator<TreeNode<InlineDiffLine<CodeLine>>> enumerator = sectionNode.GetEnumerator())
            {
                enumerator.MoveNext();
                while (enumerator.MoveNext())
                {
                    var node = enumerator.Current;
                    if (node.WasDetachedLeafParent)
                        continue;

                    if (node.Data.Kind == DiffLineKind.Added || node.Data.Kind == DiffLineKind.Removed)
                    {
                        childNodeHasDiff = true;
                        break;
                    }
                }
            }
            return childNodeHasDiff;
        }

        /// <summary>
        /// Get Section root of appriopriate RenderType from Section in RenderResult corresponding to the passed SectionId an
        /// </summary>
        public TreeNode<CodeLine> GetCodeLineSectionRoot(int sectionId, RenderType renderType = RenderType.Normal, bool showDocumentation = false, bool skipDiff = false)
        {
            RenderResult renderResult = GetRenderResult(renderType, showDocumentation, skipDiff);

            if (renderResult.Sections.Count > sectionId)
            {
                return renderResult.Sections[sectionId];
            }
            return null;
        }


        /// <summary>
        /// Get CodeLines for leafs detached from main tree.
        /// </summary>
        public CodeLine[] GetDetachedLeafSectionLines(TreeNode<CodeLine> parentNode, RenderType renderType = RenderType.Normal, bool skipDiff = false)
        {
            int leafSectionId;
            bool parseWorked = Int32.TryParse(parentNode.Data.DisplayString, out leafSectionId);
            CodeLine[] renderedLeafSection = new CodeLine[] { };

            if (parseWorked && CodeFile.LeafSections.Count > leafSectionId)
            {
                var leafSection = CodeFile.LeafSections[leafSectionId];

                if (renderType == RenderType.Normal)
                {
                    renderedLeafSection = CodeFileHtmlRenderer.Normal.Render(leafSection);
                }
                else if (renderType == RenderType.Text)
                {
                    renderedLeafSection = CodeFileHtmlRenderer.Instance.Render(leafSection, enableSkipDiff: skipDiff);
                }
                else
                {
                    renderedLeafSection = CodeFileHtmlRenderer.ReadOnly.Render(leafSection);
                }

            }
            return renderedLeafSection;
        }

        /// <summary>
        /// Getermine the appriopriate RenderResult to pull from based on passed RenderType
        /// </summary>
        private RenderResult GetRenderResult(RenderType renderType = RenderType.Normal, bool showDocumentation = false, bool skipDiff = false)
        {
            RenderResult renderResult;

            switch (renderType)
            {
                case RenderType.Text:
                    if (RenderResultText.Equals(default(RenderResult)))
                        _ = RenderText(showDocumentation, skipDiff);
                    renderResult = RenderResultText;
                    break;
                case RenderType.ReadOnly:
                    if (RenderResultReadOnly.Equals(default(RenderResult)))
                        _ = RenderReadOnly(showDocumentation);
                    renderResult = RenderResultReadOnly;
                    break;
                default:
                    if (RenderResult.Equals(default(RenderResult)))
                        _ = Render(showDocumentation);
                    renderResult = RenderResult;
                    break;
            }

            return renderResult;
        }
    }
}
