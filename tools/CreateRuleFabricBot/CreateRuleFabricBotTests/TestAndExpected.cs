namespace CreateRuleFabricBotTests
{
    public class TestAndExpected
    {
        public TestAndExpected(string pathAndOwnersLine, string labelsLine, string expectedPath, string[] expectedOwners, string[] expectedLabels)
        {
            PathAndOwners = pathAndOwnersLine;
            LabelsLine = labelsLine;

            ExpectedLabels = expectedLabels;
            ExpectedOwners = expectedOwners;
            ExpectedPath = expectedPath;
        }

        public string LabelsLine { get; set; }

        public string PathAndOwners { get; set; }

        public string[] ExpectedLabels { get; set; }

        public string[] ExpectedOwners { get; set; }

        public string ExpectedPath { get; set; }
    }
}
