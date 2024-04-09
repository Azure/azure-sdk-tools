package com.azure.tools.apiview.processor.analysers.util;

import com.azure.tools.apiview.processor.model.Token;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;

/**
 * Miscellaneous utility methods.
 */
public final class MiscUtils {
    public static final Pattern URL_MATCH = Pattern.compile(
        "https?://(www\\.)?[-a-zA-Z0-9@:%._+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b([-a-zA-Z0-9()@:%_+.~#?&/=]*)");

    public static List<String> wrap(String string, int lineLength) {
        List<String> wrappedLines = new ArrayList<>();

        for (String line : string.split("\n")) {
            wrapLine(line, lineLength, wrappedLines);
        }

        return wrappedLines;
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

    private static void wrapLine(String line, int lineLength, List<String> collector) {
        if (line.isEmpty()) {
            return;
        }

        if (line.length() <= lineLength) {
            collector.add(line);
            return;
        }

        final String[] words = line.split(" ");
        StringBuilder trimmedLine = new StringBuilder();

        for (final String word : words) {
            if (trimmedLine.length() + 1 + word.length() > lineLength) {
                collector.add(trimmedLine.toString());
                trimmedLine = new StringBuilder();
            }
            trimmedLine.append(word).append(" ");
        }

        if (trimmedLine.length() > 0) {
            collector.add(trimmedLine.toString());
        }
    }

    /**
     * Makes all characters in the string lowercase, other than the first character.
     */
    public static String upperCase(String s) {
        return upperCase(s, 0);
    }

    /**
     * Makes all characters in the string lowercase, other than the given index.
     */
    public static String upperCase(String s, int index) {
        return s.substring(0, index).toLowerCase()
                + Character.toUpperCase(s.charAt(index))
                + s.substring(index + 1).toLowerCase();
    }

    private MiscUtils() {
    }
}
