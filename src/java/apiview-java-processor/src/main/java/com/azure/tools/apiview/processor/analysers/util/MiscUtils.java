package com.azure.tools.apiview.processor.analysers.util;

import com.azure.tools.apiview.processor.model.Token;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

import static com.azure.tools.apiview.processor.model.TokenKind.MAVEN_VALUE;
import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;

/**
 * Miscellaneous utility methods.
 */
public final class MiscUtils {
    public static final Pattern URL_MATCH = Pattern.compile(
        "https?://(www\\.)?[-a-zA-Z0-9@:%._+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b([-a-zA-Z0-9()@:%_+.~#?&/=]*)");

    /**
     * Tokenizes a key-value pair.
     *
     * @param key The key.
     * @param value The value.
     * @return A token representing the key-value pair.
     */
    public static Token tokeniseMavenKeyValue(String key, Object value) {
        return new Token(MAVEN_VALUE, value == null ? "<default value>" : value.toString());//, prefix + key + "-" + value);
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
