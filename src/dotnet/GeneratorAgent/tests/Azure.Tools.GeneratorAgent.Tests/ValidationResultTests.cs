using NUnit.Framework;
using Azure.Tools.GeneratorAgent.Security;

namespace Azure.Tools.GeneratorAgent.Tests.Security
{
    [TestFixture]
    public class ValidationResultTests
    {
        [Test]
        public void Valid_WithValue_ShouldCreateValidResult()
        {
            const string testValue = "test-value";

            var result = ValidationResult.Valid(testValue);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Value, Is.EqualTo(testValue));
            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Valid_WithEmptyValue_ShouldCreateValidResult()
        {
            const string emptyValue = "";

            var result = ValidationResult.Valid(emptyValue);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Value, Is.EqualTo(emptyValue));
            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Valid_WithNullValue_ShouldCreateValidResult()
        {
            string? nullValue = null;

            var result = ValidationResult.Valid(nullValue!);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Invalid_WithErrorMessage_ShouldCreateInvalidResult()
        {
            const string errorMessage = "Validation failed";

            var result = ValidationResult.Invalid(errorMessage);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.ErrorMessage, Is.EqualTo(errorMessage));
        }

        [Test]
        public void Invalid_WithEmptyErrorMessage_ShouldCreateInvalidResult()
        {
            const string emptyErrorMessage = "";

            var result = ValidationResult.Invalid(emptyErrorMessage);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.ErrorMessage, Is.EqualTo(emptyErrorMessage));
        }

        [Test]
        public void Invalid_WithNullErrorMessage_ShouldCreateInvalidResult()
        {
            string? nullErrorMessage = null;

            var result = ValidationResult.Invalid(nullErrorMessage!);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.ErrorMessage, Is.Null);
        }

        [Test]
        public void Valid_WithLongValue_ShouldCreateValidResult()
        {
            const string longValue = "This is a very long value that should still be handled correctly by the ValidationResult class";

            var result = ValidationResult.Valid(longValue);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Value, Is.EqualTo(longValue));
            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Invalid_WithLongErrorMessage_ShouldCreateInvalidResult()
        {
            const string longErrorMessage = "This is a very long error message that should still be handled correctly by the ValidationResult class";

            var result = ValidationResult.Invalid(longErrorMessage);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.ErrorMessage, Is.EqualTo(longErrorMessage));
        }

        [Test]
        public void Valid_WithSpecialCharacters_ShouldCreateValidResult()
        {
            const string specialValue = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var result = ValidationResult.Valid(specialValue);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Value, Is.EqualTo(specialValue));
            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Invalid_WithSpecialCharacters_ShouldCreateInvalidResult()
        {
            const string specialErrorMessage = "Error: !@#$%^&*()_+-=[]{}|;:,.<>?";

            var result = ValidationResult.Invalid(specialErrorMessage);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.ErrorMessage, Is.EqualTo(specialErrorMessage));
        }
    }
}
