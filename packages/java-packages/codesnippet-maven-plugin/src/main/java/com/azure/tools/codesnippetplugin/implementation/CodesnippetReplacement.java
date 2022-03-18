// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import java.util.Objects;
import java.util.regex.Pattern;

/**
 * Represents a replacement that will be performed on codesnippets before they're injected into Javadocs or READMEs.
 */
final class CodesnippetReplacement {
    private final String target;
    private final String replacement;
    private final Pattern optionalPattern;
    private final char targetChar;

    CodesnippetReplacement(String target, String replacement) {
        this.target = Objects.requireNonNull(target);
        this.replacement = replacement;

        // If the target replacement is longer than a single character a Pattern will be used to perform the
        // replacement. Otherwise, a StringBuilder with a linear pass will be used.
        if (target.length() == 1) {
            targetChar = target.charAt(0);
            optionalPattern = null;
        } else {
            optionalPattern = Pattern.compile(target);
            targetChar = Character.MIN_VALUE;
        }
    }

    String replaceCodesnippet(String codesnippet) {
        // Codesnippet is null or empty, replacement isn't possible, so just return the codesnippet String.
        if (codesnippet == null || codesnippet.isEmpty()) {
            return codesnippet;
        }

        // Replacement target is complex, use the optional Pattern to perform replacement.
        if (optionalPattern != null) {
            return optionalPattern.matcher(codesnippet).replaceAll(replacement);
        }

        // Otherwise, use the simpler and more performant StringBuilder replacement.
        //
        // Create a StringBuilder with the initial capacity of the codesnippet length plus the size difference of 50
        // replacements of target. It will be very rare for 50 replacements to happen, so this will cover almost all
        // replacement scenarios without needing the StringBuilder to perform resizing. And, generally speaking this
        // will rarely exceed any reasonable size (1KB) so if the initial capacity is too large there will be minimal
        // impact.
        StringBuilder replacer = null;
        int prevStart = 0;

        for (int i = 0; i < codesnippet.length(); i++) {
            if (codesnippet.charAt(i) == targetChar) {
                if (replacer == null) {
                    replacer = new StringBuilder(codesnippet.length() + (replacement.length() - target.length()) * 50);
                }

                if (prevStart != i) {
                    replacer.append(codesnippet, prevStart, i);
                }
                replacer.append(replacement);

                prevStart = i + 1;
            }
        }

        if (replacer == null) {
            return codesnippet;
        }

        replacer.append(codesnippet, prevStart, codesnippet.length());

        return replacer.toString();
    }
}
