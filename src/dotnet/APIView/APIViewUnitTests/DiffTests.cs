// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using APIView.DIff;
using Xunit;

namespace APIViewUnitTests
{
    public class DiffTests
    {
        [Theory]
        [InlineData("A,B,C", "A,C,C", "A,-B,+C,C")]
        [InlineData("A", "B", "-A,+B")]
        [InlineData("A,A", "B,B", "-A,-A,+B,+B")]
        [InlineData("A,A,A", "B,A,B", "+B,A,-A,-A,+B")]
        [InlineData("A,B,C,D,D", "B,C,B", "-A,B,C,-D,-D,+B")]
        [InlineData("A,D,D,D,D,D,A,A", "A,C,C,C,C,C,A,A,B,B", "A,-D,-D,-D,-D,-D,+C,+C,+C,+C,+C,A,A,+B,+B")]
        public void DiffWorks(string before, string after, string unified)
        {
            var beforeArray = before.Split(',');
            var afterArray = after.Split(',');
            var chunks = InlineDiff.Compute(
                beforeArray,
                afterArray,
                beforeArray,
                afterArray
            );

            List<string> unifiedList = new List<string>();
            foreach (var inlineDiff in chunks)
            {
                switch (inlineDiff.Kind)
                {
                    case DiffLineKind.Unchanged:
                        unifiedList.Add(inlineDiff.Line);
                        break;
                    case DiffLineKind.Added:
                        unifiedList.Add('+' + inlineDiff.Line);
                        break;
                    case DiffLineKind.Removed:
                        unifiedList.Add('-' + inlineDiff.Line);
                        break;
                }
            }

            Assert.Equal(unified.Split(','), unifiedList.ToArray());
        }
    }
}
