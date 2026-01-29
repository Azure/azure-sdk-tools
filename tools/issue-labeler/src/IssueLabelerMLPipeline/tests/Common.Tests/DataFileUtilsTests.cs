using System;
using System.IO;

namespace Common.Tests
{
    public class DataFileUtilsTests
    {
        [Fact]
        public void EnsureOutputDirectory_ShouldCreateDirectory_WhenDirectoryDoesNotExist()
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), "testDir", "testFile.txt");
            string tempDirPath = Path.GetDirectoryName(tempFilePath)!;

            try
            {
                DataFileUtils.EnsureOutputDirectory(tempFilePath);
                Assert.True(Directory.Exists(tempDirPath));
            }
            finally
            {
                if (Directory.Exists(tempDirPath))
                {
                    Directory.Delete(tempDirPath, recursive: true);
                }
            }
        }

        [Fact]
        public void SanitizeText_ShouldReplaceSpecialCharacters()
        {
            string input = "Line1\r\nLine2\t\"Quoted\"";
            string expected = "Line1  Line2 `Quoted`";

            string result = DataFileUtils.SanitizeText(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizeTextArray_ShouldJoinAndSanitizeStrings()
        {
            string[] input = ["\tLine1\r\n", "Line2\t", "\"  Quo\ted\""];
            string expected = "Line1 Line2 `  Quo ed`";

            string result = DataFileUtils.SanitizeTextArray(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatIssueRecord_ShouldReturnTabSeparatedString()
        {
            string categoryLabel = "category-testing";
            string serviceLabel = "service-testing";
            string title = "Issue title";
            string body = "Issue body\r\nwith new line";
            string[] expected = ["category-testing","service-testing","Issue title","Issue body  with new line"];

            string[] result = DataFileUtils.FormatIssueRecord(categoryLabel, serviceLabel, title, body).Split('\t');

            Assert.Equal(expected, result);
        }
    }
}
