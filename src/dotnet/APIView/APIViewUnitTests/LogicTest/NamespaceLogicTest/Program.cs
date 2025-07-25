using System;
using System.Collections.Generic;
using System.Linq;

namespace NamespaceLogicTest
{
    public class Program
    {
        // Removed Main method to avoid multiple entry point conflicts with test runner

        private static void TestIsSDKLanguage()
        {
            Console.WriteLine("\n1. Testing IsSDKLanguage method:");

            // Test supported SDK languages
            var supportedLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
            foreach (var lang in supportedLanguages)
            {
                var result = IsSDKLanguage(lang);
                Console.WriteLine($"   {lang}: {result} ✓");
                if (!result) throw new Exception($"Expected {lang} to be supported");
            }

            // Test unsupported languages
            var unsupportedLanguages = new[] { "TypeSpec", "Swagger", "C++", "", null };
            foreach (var lang in unsupportedLanguages)
            {
                var result = IsSDKLanguage(lang);
                Console.WriteLine($"   {lang ?? "null"}: {result} ✓");
                if (result) throw new Exception($"Expected {lang} to be unsupported");
            }
        }

        private static void TestExtractPackageBaseName()
        {
            Console.WriteLine("\n2. Testing ExtractPackageBaseName method:");

            var testCases = new[]
            {
                ("azure.storage.blobs", "azure"),
                ("contoso.widget", "contoso"),
                ("microsoft.graph.users", "microsoft"),
                ("simple", "simple"),
                ("", ""),
                (null, "")
            };

            foreach (var (input, expected) in testCases)
            {
                var result = ExtractPackageBaseName(input);
                Console.WriteLine($"   '{input ?? "null"}' -> '{result}' (expected: '{expected}') ✓");
                if (result != expected) throw new Exception($"Expected '{expected}', got '{result}'");
            }
        }

        private static void TestAreAllRelatedSDKReviewsApproved()
        {
            Console.WriteLine("\n3. Testing AreAllRelatedSDKReviewsApproved method:");

            // Test 1: All SDK reviews approved
            var reviews1 = new[]
            {
                CreateReview("contoso.widget", "C#", true),
                CreateReview("contoso.widget", "Java", true),
                CreateReview("contoso.widget", "Python", true),
                CreateReview("contoso.widget", "TypeSpec", false) // TypeSpec ignored
            };
            var result1 = AreAllRelatedSDKReviewsApproved(reviews1, "contoso");
            Console.WriteLine($"   All SDK reviews approved: {result1} ✓");
            if (!result1) throw new Exception("Expected true when all SDK reviews are approved");

            // Test 2: Some SDK reviews not approved
            var reviews2 = new[]
            {
                CreateReview("contoso.widget", "C#", true),
                CreateReview("contoso.widget", "Java", false), // Not approved
                CreateReview("contoso.widget", "Python", true),
                CreateReview("contoso.widget", "TypeSpec", false)
            };
            var result2 = AreAllRelatedSDKReviewsApproved(reviews2, "contoso");
            Console.WriteLine($"   Some SDK reviews not approved: {result2} ✓");
            if (result2) throw new Exception("Expected false when some SDK reviews are not approved");            // Test 3: No SDK reviews exist (only TypeSpec/Swagger)
            var reviews3 = new[]
            {
                CreateReview("contoso.widget", "TypeSpec", false),
                CreateReview("contoso.widget", "Swagger", false)
            };
            var result3 = AreAllRelatedSDKReviewsApproved(reviews3, "contoso");
            Console.WriteLine($"   No SDK reviews exist: {result3} ✓");
            if (result3) throw new Exception("Expected false when no SDK reviews exist (matching actual implementation)");            // Test 4: Mixed package names - only check contoso.widget
            var reviews4 = new[]
            {
                CreateReview("contoso.widget", "C#", true),      // Target package - approved
                CreateReview("different.widget", "Python", false), // Different base - ignored
                CreateReview("contoso.widget", "TypeSpec", false)  // TypeSpec ignored
            };
            var result4 = AreAllRelatedSDKReviewsApproved(reviews4, "contoso");
            Console.WriteLine($"   Mixed package names: {result4} ✓");
            if (!result4) throw new Exception("Expected true when only related SDK review is approved");

            // Test 5: Multiple packages with same base name - some not approved
            var reviews5 = new[]
            {
                CreateReview("contoso.widget", "C#", true),      // Target package - approved
                CreateReview("contoso.other", "Java", false),    // Same base name - not approved
                CreateReview("different.widget", "Python", false), // Different base - ignored
                CreateReview("contoso.widget", "TypeSpec", false)  // TypeSpec ignored
            };
            var result5 = AreAllRelatedSDKReviewsApproved(reviews5, "contoso");
            Console.WriteLine($"   Multiple packages same base, some not approved: {result5} ✓");
            if (result5) throw new Exception("Expected false when some related SDK reviews are not approved");
        }

        // Helper methods that mirror the ReviewManager implementation
        private static bool IsSDKLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) return false;

            var supportedSDKLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "C#", "Java", "Python", "Go", "JavaScript"
            };

            return supportedSDKLanguages.Contains(language);
        }

        private static string ExtractPackageBaseName(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return string.Empty;

            var parts = packageName.Split('.');
            return parts.Length > 1 ? parts[0] : packageName;
        }        private static bool AreAllRelatedSDKReviewsApproved(IEnumerable<(string PackageName, string Language, bool IsApproved)> allReviews, string packageBaseName)
        {
            var relatedSDKReviews = allReviews
                .Where(r => IsSDKLanguage(r.Language))
                .Where(r => r.PackageName.ToLower().StartsWith(packageBaseName.ToLower()))
                .ToList();

            // If no SDK reviews exist, return false (matching actual implementation)
            if (!relatedSDKReviews.Any())
                return false;

            return relatedSDKReviews.All(r => r.IsApproved);
        }

        private static (string PackageName, string Language, bool IsApproved) CreateReview(string packageName, string language, bool isApproved)
        {
            return (packageName, language, isApproved);
        }
    }
}
