using System;
using NUnit.Framework;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;

namespace Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers.Tests
{
    [TestFixture]
    public class AZC0012RuleAnalyzerTests
    {
        private const string BasicErrorMessage = "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'.";
        private const string ComplexTypeErrorMessage = "Type name 'Data' is too generic. Consider using a more descriptive multi-word name, such as 'UserData'.";
        private const string NumberedTypeErrorMessage = "Type name 'Service2' is too generic. Consider using a more descriptive multi-word name, such as 'AccountService2'.";
        private const string SpecialCharacterErrorMessage = "Type name 'My_Service' is too generic. Consider using a more descriptive multi-word name, such as 'MyCustomService'.";
        private const string EllipsisErrorMessage = "Type name 'Helper' is too generic… Consider using a more descriptive multi-word name, such as 'DatabaseHelper'.";
        private const string MultilineErrorMessage = @"Type name 'Manager' is too generic. 
Consider using a more descriptive multi-word name, such as 'ResourceManager'.";
        private const string ComplexGenericErrorMessage = "Type name 'MyNamespace.ComplexGenericType<T>' is too generic. Consider using a more descriptive multi-word name, such as 'MyNamespace.SpecificServiceClient<T>'.";
        private const string SpecialCharsInNameErrorMessage = "Type name 'Client_$Test123' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient_$Test123'.";
        private const string InvalidPatternMessage = "This message does not match the expected pattern.";
        private const string EmptyTypeNamesMessage = "Type name '' is too generic. Consider using a more descriptive multi-word name, such as ''.";
        private const string WhitespaceTypeNamesMessage = "Type name '   ' is too generic. Consider using a more descriptive multi-word name, such as '   '.";
        private const string PartialMatchMessage = "Type name 'Client' is too generic.";

        private const string ExpectedOriginalName = "Client";
        private const string ExpectedNewName = "ServiceClient";

        [Test]
        public void CanFix_AZC0012()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Some error message");

            bool canFix = analyzer.CanFix(error);

            Assert.That(canFix, Is.True);
        }

        [Test]
        public void CanFix_AZC0012_CaseInsensitive()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("azc0012", "Some error message");

            bool canFix = analyzer.CanFix(error);

            Assert.That(canFix, Is.True);
        }

        [Test]
        public void CanFix_DifferentErrorType()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0013", "Some error message");

            bool canFix = analyzer.CanFix(error);

            Assert.That(canFix, Is.False);
        }

        [Test]
        public void GetFix_BasicPattern()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", BasicErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo(ExpectedOriginalName));
            Assert.That(renameFix.NewName, Is.EqualTo(ExpectedNewName));
            Assert.That(renameFix.Action, Is.EqualTo(FixAction.Rename));
        }

        [Test]
        public void GetFix_ComplexTypeName()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", ComplexTypeErrorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("Data"));
            Assert.That(renameFix.NewName, Is.EqualTo("UserData"));
        }

        [Test]
        public void GetFix_TypeNameWithNumbers()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            string errorMessage = "Type name 'Service2' is too generic. Consider using a more descriptive multi-word name, such as 'AccountService2'.";
            RuleError error = new RuleError("AZC0012", errorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("Service2"));
            Assert.That(renameFix.NewName, Is.EqualTo("AccountService2"));
        }

        [Test]
        public void GetFix_TypeNameWithSpecialCharacters()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            string errorMessage = "Type name 'My_Service' is too generic. Consider using a more descriptive multi-word name, such as 'MyCustomService'.";
            RuleError error = new RuleError("AZC0012", errorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("My_Service"));
            Assert.That(renameFix.NewName, Is.EqualTo("MyCustomService"));
        }

        [Test]
        public void GetFix_MessageWithEllipsis()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            string errorMessage = "Type name 'Helper' is too generic… Consider using a more descriptive multi-word name, such as 'DatabaseHelper'.";
            RuleError error = new RuleError("AZC0012", errorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("Helper"));
            Assert.That(renameFix.NewName, Is.EqualTo("DatabaseHelper"));
        }

        [Test]
        public void GetFix_MultilineMessage()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            string errorMessage = @"Type name 'Manager' is too generic. 
Consider using a more descriptive multi-word name, such as 'ResourceManager'.";
            RuleError error = new RuleError("AZC0012", errorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("Manager"));
            Assert.That(renameFix.NewName, Is.EqualTo("ResourceManager"));
        }

        [Test]
        public void GetFix_PatternDoesNotMatch()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", InvalidPatternMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [TestCase("AZC0012")]
        [TestCase("azc0012")]
        [TestCase("AzC0012")]
        public void CanFix_VariousCaseFormats(string errorType)
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError(errorType, "Some message");

            bool canFix = analyzer.CanFix(error);

            Assert.That(canFix, Is.True);
        }

        [TestCase("AZC0011")]
        [TestCase("AZC0013")]
        [TestCase("CS0012")]
        [TestCase("INVALID")]
        public void CanFix_InvalidErrorTypes(string errorType)
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError(errorType, "Some message");

            bool canFix = analyzer.CanFix(error);

            Assert.That(canFix, Is.False);
        }

        [Test]
        public void CanFix_EmptyErrorType_ThrowsArgumentException()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            
            Assert.Throws<ArgumentException>(() => new RuleError("", "Some message"));
        }

        [Test]
        public void CanFix_WithNullError_ThrowsArgumentNullException()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();

            Assert.Throws<ArgumentNullException>(() => analyzer.CanFix(null!));
        }

        [Test]
        public void GetFix_WithNullError_ThrowsArgumentNullException()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();

            Assert.Throws<ArgumentNullException>(() => analyzer.GetFix(null!));
        }

        [Test]
        public void GetFix_WithUnsupportedErrorType_ReturnsNull()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0013", "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'.");

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_WithMalformedMessage_ReturnsNull()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", InvalidPatternMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_WithEmptyTypeNames_ReturnsNull()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", EmptyTypeNamesMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_WithWhitespaceTypeNames_ReturnsNull()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Type name '   ' is too generic. Consider using a more descriptive multi-word name, such as '   '.");

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_WithPartialMatch_ReturnsNull()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            RuleError error = new RuleError("AZC0012", "Type name 'Client' is too generic.");

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Null);
        }

        [Test]
        public void GetFix_WithComplexTypeName_HandlesCorrectly()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            string errorMessage = "Type name 'MyNamespace.ComplexGenericType<T>' is too generic. Consider using a more descriptive multi-word name, such as 'MyNamespace.SpecificServiceClient<T>'.";
            RuleError error = new RuleError("AZC0012", errorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("MyNamespace.ComplexGenericType<T>"));
            Assert.That(renameFix.NewName, Is.EqualTo("MyNamespace.SpecificServiceClient<T>"));
        }

        [Test]
        public void GetFix_WithSpecialCharactersInTypeName_HandlesCorrectly()
        {
            AZC0012RuleAnalyzer analyzer = new AZC0012RuleAnalyzer();
            string errorMessage = "Type name 'Client_$Test123' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient_$Test123'.";
            RuleError error = new RuleError("AZC0012", errorMessage);

            Fix? fix = analyzer.GetFix(error);

            Assert.That(fix, Is.Not.Null);
            Assert.That(fix, Is.InstanceOf<RenameFix>());
            
            RenameFix renameFix = (RenameFix)fix!;
            Assert.That(renameFix.OriginalName, Is.EqualTo("Client_$Test123"));
            Assert.That(renameFix.NewName, Is.EqualTo("ServiceClient_$Test123"));
        }
    }
}
