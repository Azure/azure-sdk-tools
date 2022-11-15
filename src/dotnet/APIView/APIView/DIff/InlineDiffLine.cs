// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
namespace APIView.DIff
{
    public struct InlineDiffLine<T>
    {
        public InlineDiffLine(T line, DiffLineKind kind, bool isHeadingWithDiffInSection = false)
        {
            Line = line;
            Kind = kind;
            OtherLine = default(T);
            IsHeadingWithDiffInSection = isHeadingWithDiffInSection;
        }

        public InlineDiffLine(T lineA, T lineB, DiffLineKind kind, bool isHeadingWithDiffInSection = false)
        {
            Line = lineA;
            OtherLine = lineB;
            Kind = kind;
            IsHeadingWithDiffInSection = isHeadingWithDiffInSection;
        }

        public T Line { get; }
        public T OtherLine { get; }
        public DiffLineKind Kind { get; }
        public bool IsHeadingWithDiffInSection { get; }

        public override string ToString()
        {
            return Kind switch
            {
                DiffLineKind.Added => "+",
                DiffLineKind.Removed => "-",
                _ => " "
            } + Line;
        }
    }
}
