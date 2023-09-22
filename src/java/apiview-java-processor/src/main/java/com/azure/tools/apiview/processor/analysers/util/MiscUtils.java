package com.azure.tools.apiview.processor.analysers.util;

import com.azure.tools.apiview.processor.model.Token;

import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;

/**
 * Miscellaneous utility methods.
 */
public final class MiscUtils {

    public static final String LINEBREAK = "\n"; // or "\r\n";
    private static final Pattern QUOTED_LINEBREAK = Pattern.compile(Pattern.quote(LINEBREAK));

    public static final Pattern URL_MATCH = Pattern.compile("https?:\\/\\/(www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b([-a-zA-Z0-9()@:%_\\+.~#?&//=]*)");

    public static String escapeHTML(final String s) {
        final StringBuilder out = new StringBuilder(Math.max(16, s.length()));
        for (int i = 0; i < s.length(); i++) {
            final char c = s.charAt(i);
            if (c > 127 || c == '"' || c == '\'' || c == '<' || c == '>' || c == '&') {
                out.append("&#");
                out.append((int) c);
                out.append(';');
            } else {
                out.append(c);
            }
        }
        return out.toString();
    }

    public static String wrap(final String string, final int lineLength) {
        final StringBuilder b = new StringBuilder();
        for (final String line : QUOTED_LINEBREAK.split(string)) {
            b.append(wrapLine(line, lineLength));
        }
        return b.toString();
    }

    /**
     * Tokenizes a key-value pair.
     *
     * @param key The key.
     * @param value The value.
     * @return A token representing the key-value pair.
     */
    public static Token tokeniseKeyValue(String key, Object value) {
        return tokeniseKeyValue(key, value, null);
    }

    /**
     * Tokenizes a key-value pair with prefix for the definition identifier.
     *
     * @param key The key.
     * @param value The value.
     * @param prefix The definition identifier prefix.
     * @return A token representing the key-value pair.
     */
    public static Token tokeniseKeyValue(String key, Object value, String prefix) {
        prefix = prefix == null || prefix.isEmpty() ? "" : prefix + "-";
        return new Token(TEXT, value == null ? "<default value>" : value.toString(), prefix + key + "-" + value);
    }

    private static String wrapLine(final String line, final int lineLength) {
        if (line.isEmpty()) return LINEBREAK;
        if (line.length() <= lineLength) return line + LINEBREAK;

        final String[] words = line.split(" ");
        final StringBuilder allLines = new StringBuilder();
        StringBuilder trimmedLine = new StringBuilder();

        for (final String word : words) {
            if (trimmedLine.length() + 1 + word.length() > lineLength) {
                allLines.append(trimmedLine).append(LINEBREAK);
                trimmedLine = new StringBuilder();
            }
            trimmedLine.append(word).append(" ");
        }

        if (trimmedLine.length() > 0) {
            allLines.append(trimmedLine);
        }

        allLines.append(LINEBREAK);
        return allLines.toString();
    }

    private MiscUtils() {
    }
}
