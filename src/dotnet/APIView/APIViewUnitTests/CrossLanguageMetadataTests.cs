using System.Text.Json;
using ApiView;
using APIView.Model;
using FluentAssertions;
using Xunit;

namespace APIViewUnitTests;

public class CrossLanguageMetadataTests
{
    [Fact]
    public void CrossLanguageMetadata_Should_Deserialize_From_Full_CodeFile_JSON()
    {
        string fullCodeFileJson = """
          {
            "PackageName": "apiview-test-codefile",
            "PackageVersion": "1.1.0",
            "ParserVersion": "0.3.23",
            "Language": "Python",
            "ReviewLines": [
              {
                "Tokens": [
                  {
                    "Kind": 0,
                    "Value": "# Package is parsed using apiview-stub-generator(version:0.3.23), Python version: 3.10.12",
                    "HasPrefixSpace": false,
                    "HasSuffixSpace": false,
                    "SkipDiff": true
                  }
                ],
                "IsContextEndLine": false,
                "LineId": "GLOBAL"
              },
              {
                "Tokens": [
                  {
                    "Kind": 2,
                    "Value": "namespace",
                    "HasPrefixSpace": false,
                    "HasSuffixSpace": true
                  },
                  {
                    "Kind": 0,
                    "Value": "apiview.test.codefile",
                    "HasPrefixSpace": false,
                    "HasSuffixSpace": false,
                    "SkipDiff": false,
                    "NavigationDisplayName": "apiview.test.codefile",
                    "RenderClasses": ["namespace"]
                  }
                ],
                "LineId": "apiview.test.codefile",
                "IsContextEndLine": false,
                "CrossLanguageId": "ApiViewTest.ApiViewTestClient"
              }
            ],
            "CrossLanguageMetadata": {
              "CrossLanguagePackageId": "ApiViewTest",
              "CrossLanguageDefinitionId": {
                "apiview.test.codefile.models.RadiologyInsightsInference": "ApiViewTest.RadiologyInsightsInference",
                "apiview.test.codefile.models.AgeMismatchInference": "ApiViewTest.AgeMismatchInference",
                "apiview.test.codefile.models.Element": "Fhir.R4.Element",
                "apiview.test.codefile.models.Annotation": "Fhir.R4.Annotation",
                "apiview.test.codefile.models.AssessmentValueRange": "ApiViewTest.AssessmentValueRange",
                "apiview.test.codefile.models.CodeableConcept": "Fhir.R4.CodeableConcept",
                "apiview.test.codefile.models.Coding": "Fhir.R4.Coding",
                "apiview.test.codefile.models.CompleteOrderDiscrepancyInference": "ApiViewTest.CompleteOrderDiscrepancyInference",
                "apiview.test.codefile.models.CriticalResult": "ApiViewTest.CriticalResult",
                "apiview.test.codefile.models.CriticalResultInference": "ApiViewTest.CriticalResultInference",
                "apiview.test.codefile.models.DocumentAdministrativeMetadata": "ApiViewTest.DocumentAdministrativeMetadata",
                "apiview.test.codefile.models.DocumentAuthor": "ApiViewTest.DocumentAuthor",
                "apiview.test.codefile.models.DocumentContent": "ApiViewTest.DocumentContent",
                "apiview.test.codefile.models.Resource": "Fhir.R4.Resource",
                "apiview.test.codefile.models.DomainResource": "Fhir.R4.DomainResource",
                "apiview.test.codefile.models.Extension": "Fhir.R4.Extension",
                "apiview.test.codefile.models.FindingInference": "ApiViewTest.FindingInference",
                "apiview.test.codefile.models.FindingOptions": "ApiViewTest.FindingOptions",
                "apiview.test.codefile.models.FollowupCommunicationInference": "ApiViewTest.FollowupCommunicationInference",
                "apiview.test.codefile.models.FollowupRecommendationInference": "ApiViewTest.FollowupRecommendationInference",
                "apiview.test.codefile.models.PatientSex": "ApiViewTest.PatientSex",
                "apiview.test.codefile.models.EncounterClass": "ApiViewTest.EncounterClass",
                "apiview.test.codefile.models.DocumentType": "ApiViewTest.DocumentType",
                "apiview.test.codefile.models.ClinicalDocumentType": "ApiViewTest.ClinicalDocumentType",
                "apiview.test.codefile.models.SpecialtyType": "ApiViewTest.SpecialtyType",
                "apiview.test.codefile.models.DocumentContentSourceType": "ApiViewTest.DocumentContentSourceType",
                "apiview.test.codefile.models.RadiologyInsightsInferenceType": "ApiViewTest.RadiologyInsightsInferenceType",
                "apiview.test.codefile.ApiViewTestClient.begin_infer_radiology_insights": "ClientForApiViewTest.ApiViewTestClient.inferRadiologyInsights",
                "apiview.test.codefile.aio.ApiViewTestClient.begin_infer_radiology_insights": "ClientForApiViewTest.ApiViewTestClient.inferRadiologyInsights"
              }
            },
            "Diagnostics": []
          }
          """;

        CodeFile codeFile = JsonSerializer.Deserialize<CodeFile>(fullCodeFileJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        CrossLanguageMetadata result = codeFile.CrossLanguageMetadata;
        result.Should().NotBeNull();
        result.CrossLanguagePackageId.Should().Be("ApiViewTest");
        result.CrossLanguageDefinitionId.Should().NotBeNull();
        result.CrossLanguageDefinitionId.Should().HaveCount(29);

        result.CrossLanguageDefinitionId.Should().ContainKey("apiview.test.codefile.models.AgeMismatchInference");
        result.CrossLanguageDefinitionId["apiview.test.codefile.models.AgeMismatchInference"]
            .Should().Be("ApiViewTest.AgeMismatchInference");

        result.CrossLanguageDefinitionId.Should().ContainKey("apiview.test.codefile.models.Element");
        result.CrossLanguageDefinitionId["apiview.test.codefile.models.Element"]
            .Should().Be("Fhir.R4.Element");

        result.CrossLanguageDefinitionId.Should().ContainKey("apiview.test.codefile.models.Annotation");
        result.CrossLanguageDefinitionId["apiview.test.codefile.models.Annotation"]
            .Should().Be("Fhir.R4.Annotation");
    }

    [Fact]
    public void CodeFile_Should_Handle_Null_CrossLanguageMetadata_Gracefully()
    {
        string codeFileJsonWithNullCrossLanguage = """
        {
            "PackageName": "apiview-test-codefile",
            "PackageVersion": "1.1.0",
            "ParserVersion": "0.3.23",
            "Language": "Python",
            "ReviewLines": [
            {
                "Tokens": [
                {
                    "Kind": 0,
                    "Value": "# Test content",
                    "HasPrefixSpace": false,
                    "HasSuffixSpace": false
                }
                ],
                "IsContextEndLine": false,
                "LineId": "TEST"
            }
            ],
            "Diagnostics": []
        }
        """;

        CodeFile result = JsonSerializer.Deserialize<CodeFile>(codeFileJsonWithNullCrossLanguage,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result.PackageName.Should().Be("apiview-test-codefile");
        result.CrossLanguageMetadata.Should().BeNull();
    }
}
