// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
namespace APIView.DIff
{
    public struct InlineDiffLine<T>
    {
        public InlineDiffLine(T line, DiffLineKind kind)
        {
            Line = line;
            Kind = kind;
        }

        public T Line { get; }
        public DiffLineKind Kind { get; }

        public override string ToString()
        {
            return Kind switch
            {
                DiffLineKind.Added => "+",
                DiffLineKind.Removed => "-",
                DiffLineKind.Unchanged => " "
            } + Line;
        }
    }
}