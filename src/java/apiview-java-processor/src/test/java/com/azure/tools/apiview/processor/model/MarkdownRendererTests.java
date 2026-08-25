package com.azure.tools.apiview.processor.model;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;

public class MarkdownRendererTests {
    @Test
    public void rendersNestedApiWithoutDocumentation() {
        APIListing listing = new APIListing();
        ReviewLine module = listing.addChildLine()
            .addToken(TokenKind.KEYWORD, "module")
            .addToken(TokenKind.MODULE_NAME, "com.azure.widgets")
            .addContextStartTokens();

        module.addChildLine()
            .addToken(new ReviewToken(TokenKind.JAVADOC, "/** Widget client. */").setDocumentation());

        module.addChildLine()
            .addToken(TokenKind.KEYWORD, "public")
            .addToken(TokenKind.RETURN_TYPE, "Widget")
            .addToken(TokenKind.METHOD_NAME, "getWidget", Spacing.NO_SPACE)
            .addToken(TokenKind.PUNCTUATION, "(", Spacing.NO_SPACE)
            .addToken(TokenKind.PARAMETER_TYPE, "String")
            .addToken(TokenKind.PARAMETER_NAME, "name", Spacing.NO_SPACE)
            .addToken(TokenKind.PUNCTUATION, ")", Spacing.NO_SPACE);

        String expected = String.join(System.lineSeparator(),
            "```java",
            "module com.azure.widgets {",
            "    public Widget getWidget(String name)",
            "```");

        String markdown = MarkdownRenderer.render(listing);

        assertEquals(expected, markdown);
        assertFalse(markdown.contains("Widget client"));
    }

    @Test
    public void removesDocumentationTokensFromMixedLines() {
        APIListing listing = new APIListing();
        listing.addChildLine()
            .addToken(new ReviewToken(TokenKind.JAVADOC, "documentation").setDocumentation())
            .addToken(TokenKind.KEYWORD, "public")
            .addToken(TokenKind.TYPE_NAME, "Widget", Spacing.NO_SPACE);

        assertEquals(String.join(System.lineSeparator(),
            "```java",
            "public Widget",
            "```"), MarkdownRenderer.render(listing));
    }
}
