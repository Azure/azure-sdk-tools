import com.azure.ai.textanalytics.TextAnalyticsAsyncClient;
import com.azure.ai.textanalytics.TextAnalyticsClient;
import com.azure.ai.textanalytics.TextAnalyticsClientBuilder;
import com.azure.ai.textanalytics.models.DetectedLanguage;
import com.azure.ai.textanalytics.models.DocumentSentiment;
import com.azure.ai.textanalytics.models.TextAnalyticsError;
import com.azure.ai.textanalytics.models.TextDocumentInput;
import com.azure.core.credential.TokenCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

public class AzureAiTextAnalyticsJavaSampleExample {

    public static void main(String[] args) throws InterruptedException {
        String endpoint = System.getenv("AZURE_TEXT_ANALYTICS_ENDPOINT");
        if (endpoint == null || endpoint.isBlank()) {
            System.err.println("Environment variable AZURE_TEXT_ANALYTICS_ENDPOINT must be set.");
            return;
        }

        TokenCredential credential;
        try {
            credential = new DefaultAzureCredentialBuilder().build();
        } catch (Exception e) {
            System.err.println("Failed to create default Azure credential: " + e.getMessage());
            return;
        }

        TextAnalyticsClient client;
        try {
            client = new TextAnalyticsClientBuilder()
                .credential(credential)
                .endpoint(endpoint)
                .buildClient();
        } catch (Exception e) {
            System.err.println("Failed to create TextAnalyticsClient: " + e.getMessage());
            return;
        }

        TextAnalyticsAsyncClient asyncClient;
        try {
            asyncClient = new TextAnalyticsClientBuilder()
                .credential(credential)
                .endpoint(endpoint)
                .buildAsyncClient();
        } catch (Exception e) {
            System.err.println("Failed to create TextAnalyticsAsyncClient: " + e.getMessage());
            return;
        }

        String singleLanguageText = "Bonjour tout le monde";
        List<TextDocumentInput> multiLanguageDocuments = List.of(
            new TextDocumentInput("1", "This is written in English.").setLanguage("en"),
            new TextDocumentInput("2", "Este es un documento escrito en Español.").setLanguage("es"),
            new TextDocumentInput("3", "Je suis très heureux.").setLanguage("fr")
        );

        detectLanguageSample(client, singleLanguageText, multiLanguageDocuments);

        analyzeSentimentSample(client);

        recognizeEntitiesSample(client);

        extractKeyPhrasesSample(client);

        recognizePiiEntitiesSample(client);

        asyncSentimentAnalysisSample(asyncClient);
    }

    private static void detectLanguageSample(TextAnalyticsClient client, String singleDoc, List<TextDocumentInput> multiDocs) {
        System.out.println("== Language Detection Sample ==");

        try {
            DetectedLanguage detectedLanguage = client.detectLanguage(singleDoc);
            System.out.printf("Single document detected language: %s (%s), confidence: %.2f%n",
                detectedLanguage.getName(), detectedLanguage.getIso6391Name(), detectedLanguage.getConfidenceScore());

            Iterable<com.azure.ai.textanalytics.models.DetectedLanguageResult> results = client.detectLanguageBatch(multiDocs, null);
            for (var result : results) {
                if (!result.isError()) {
                    DetectedLanguage lang = result.getPrimaryLanguage();
                    System.out.printf("Doc ID: %s, Language: %s (%s), Confidence: %.2f%n",
                        result.getId(), lang.getName(), lang.getIso6391Name(), lang.getConfidenceScore());
                } else {
                    TextAnalyticsError error = result.getError();
                    System.err.printf("Doc ID: %s, Error: %s - %s%n", result.getId(), error.getErrorCode(), error.getMessage());
                }
            }
        } catch (Exception ex) {
            System.err.println("Error during language detection: " + ex.getMessage());
        }
        System.out.println();
    }

    private static void analyzeSentimentSample(TextAnalyticsClient client) {
        System.out.println("== Sentiment Analysis Sample ==");
        String document = "The hotel was dark and unclean. I like Microsoft.";

        try {
            DocumentSentiment sentiment = client.analyzeSentiment(document);
            System.out.printf("Overall sentiment: %s%n", sentiment.getSentiment());
            sentiment.getSentences().forEach(sentenceSentiment ->
                System.out.printf("Sentence sentiment: %s. Positive %.2f, Neutral %.2f, Negative %.2f%n",
                    sentenceSentiment.getSentiment(),
                    sentenceSentiment.getConfidenceScores().getPositive(),
                    sentenceSentiment.getConfidenceScores().getNeutral(),
                    sentenceSentiment.getConfidenceScores().getNegative()));
        } catch (Exception ex) {
            System.err.println("Error during sentiment analysis: " + ex.getMessage());
        }
        System.out.println();
    }

    private static void recognizeEntitiesSample(TextAnalyticsClient client) {
        System.out.println("== Named Entity Recognition Sample ==");
        String document = "Satya Nadella is the CEO of Microsoft.";

        try {
            Iterable<com.azure.ai.textanalytics.models.CategorizedEntity> entities = client.recognizeEntities(document);
            for (com.azure.ai.textanalytics.models.CategorizedEntity entity : entities) {
                System.out.printf("Entity: %s, Category: %s, Subcategory: %s, Confidence: %.2f%n",
                    entity.getText(),
                    entity.getCategory(),
                    entity.getSubcategory() == null ? "N/A" : entity.getSubcategory(),
                    entity.getConfidenceScore());
            }
        } catch (Exception ex) {
            System.err.println("Error during entity recognition: " + ex.getMessage());
        }
        System.out.println();
    }

    private static void extractKeyPhrasesSample(TextAnalyticsClient client) {
        System.out.println("== Key Phrase Extraction Sample ==");
        String document = "My cat might need to see a veterinarian.";

        try {
            Iterable<String> keyPhrases = client.extractKeyPhrases(document);
            for (String phrase : keyPhrases) {
                System.out.printf("Key phrase: %s%n", phrase);
            }
        } catch (Exception ex) {
            System.err.println("Error during key phrase extraction: " + ex.getMessage());
        }
        System.out.println();
    }

    private static void recognizePiiEntitiesSample(TextAnalyticsClient client) {
        System.out.println("== PII Entity Recognition and Redaction Sample ==");
        String document = "My SSN is 859-98-0987.";

        try {
            com.azure.ai.textanalytics.models.PiiEntityCollection piiEntities = client.recognizePiiEntities(document);
            System.out.printf("Redacted text: %s%n", piiEntities.getRedactedText());
            for (com.azure.ai.textanalytics.models.PiiEntity pii : piiEntities) {
                System.out.printf("PII entity: %s, Category: %s, Subcategory: %s, Confidence: %.2f%n",
                    pii.getText(),
                    pii.getCategory(),
                    pii.getSubcategory() == null ? "N/A" : pii.getSubcategory(),
                    pii.getConfidenceScore());
            }
        } catch (Exception ex) {
            System.err.println("Error during PII recognition: " + ex.getMessage());
        }
        System.out.println();
    }

    private static void asyncSentimentAnalysisSample(TextAnalyticsAsyncClient asyncClient) throws InterruptedException {
        System.out.println("== Async Sentiment Analysis Batch Sample ==");
        List<TextDocumentInput> documents = new ArrayList<>();
        documents.add(new TextDocumentInput("1", "The food was delicious and the service was excellent.").setLanguage("en"));
        documents.add(new TextDocumentInput("2", "Das Essen war schrecklich und der Service schlecht.").setLanguage("de"));

        CountDownLatch latch = new CountDownLatch(1);

        asyncClient.analyzeSentimentBatch(documents)
            .subscribe(result -> {
                if (!result.isError()) {
                    DocumentSentiment sentiment = result.getDocumentSentiment();
                    System.out.printf("Doc ID: %s, Sentiment: %s%n", result.getId(), sentiment.getSentiment());
                } else {
                    TextAnalyticsError error = result.getError();
                    System.err.printf("Doc ID: %s, Error: %s - %s%n", result.getId(), error.getErrorCode(), error.getMessage());
                }
            }, error -> {
                System.err.println("Error in async operation: " + error.getMessage());
                latch.countDown();
            }, latch::countDown);

        if (!latch.await(30, TimeUnit.SECONDS)) {
            System.err.println("Async processing timed out.");
        }
        System.out.println();
    }
}