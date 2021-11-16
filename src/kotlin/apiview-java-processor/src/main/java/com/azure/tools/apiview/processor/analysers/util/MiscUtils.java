package com.azure.tools.apiview.processor.analysers.util;

import java.util.regex.Pattern;

public class MiscUtils {

    public static final String LINEBREAK = "\n"; // or "\r\n";

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
        for (final String line : string.split(Pattern.quote(LINEBREAK))) {
            b.append(wrapLine(line, lineLength));
        }
        return b.toString();
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
}
