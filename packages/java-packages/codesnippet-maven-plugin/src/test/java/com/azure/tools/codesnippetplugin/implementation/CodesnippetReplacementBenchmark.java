// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.tools.codesnippetplugin.implementation;

import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;
import org.openjdk.jmh.infra.Blackhole;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.concurrent.TimeUnit;
import java.util.regex.Pattern;

import static com.azure.tools.codesnippetplugin.implementation.SnippetReplacer.JAVADOC_CODESNIPPET_REPLACEMENTS;

@Fork(3)
@Warmup(iterations = 3, time = 2)
@Measurement(iterations = 3, time = 10)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@State(Scope.Thread)
public class CodesnippetReplacementBenchmark {
    private static final List<String> CODESNIPPET_LINES = new ArrayList<String>() {{
        add("// A supplier that fetches the first page of data from source/service");
        add("Supplier<Mono<PagedResponse<Integer>>> firstPageRetriever = () -> getFirstPage();");
        add("");
        add("// A function that fetches subsequent pages of data from source/service given a continuation token");
        add("Function<String, Mono<PagedResponse<Integer>>> nextPageRetriever =");
        add("    continuationToken -> getNextPage(continuationToken);");
        add("");
        add("PagedFlux<Integer> pagedFlux = new PagedFlux<>(firstPageRetriever,");
        add("    nextPageRetriever);");
    }};

    private static final Map<Pattern, String> PATTERN_REPLACEMENT = new LinkedHashMap<Pattern, String>() {{
        put(Pattern.compile("&"), "&amp;");
        put(Pattern.compile("\""), "&quot;");
        put(Pattern.compile(">"), "&gt;");
        put(Pattern.compile("<"), "&lt;");
        put(Pattern.compile("@"), "&#64;");
        put(Pattern.compile("\\{"), "&#123;");
        put(Pattern.compile("}"), "&#125;");
        put(Pattern.compile("\\("), "&#40;");
        put(Pattern.compile("\\)"), "&#41;");
        put(Pattern.compile("/"), "&#47;");
        put(Pattern.compile("\\\\"), "&#92;");
    }};

    private static final List<CodesnippetReplacementPlayground> CODESNIPPET_PLAYGROUND_REPLACEMENT = new ArrayList<CodesnippetReplacementPlayground>() {{
        add(new CodesnippetReplacementPlayground("&", "&amp;"));
        add(new CodesnippetReplacementPlayground("\"", "&quot;"));
        add(new CodesnippetReplacementPlayground(">", "&gt;"));
        add(new CodesnippetReplacementPlayground("<", "&lt;"));
        add(new CodesnippetReplacementPlayground("@", "&#64;"));
        add(new CodesnippetReplacementPlayground("\\{", "&#123;"));
        add(new CodesnippetReplacementPlayground("}", "&#125;"));
        add(new CodesnippetReplacementPlayground("\\(", "&#40;"));
        add(new CodesnippetReplacementPlayground("\\)", "&#41;"));
        add(new CodesnippetReplacementPlayground("/", "&#47;"));
        add(new CodesnippetReplacementPlayground("\\\\", "&#92;"));
    }};

    @Benchmark
    public void patternReplacement(Blackhole blackhole) {
        for (String codesnippetLine : CODESNIPPET_LINES) {
            if (codesnippetLine.isEmpty()) {
                continue;
            }

            for (Map.Entry<Pattern, String> replacement : PATTERN_REPLACEMENT.entrySet()) {
                codesnippetLine = replacement.getKey().matcher(codesnippetLine).replaceAll(replacement.getValue());
            }

            blackhole.consume(codesnippetLine);
        }
    }

    @Benchmark
    public void codesnippetReplacement(Blackhole blackhole) {
        for (String codesnippetLine : CODESNIPPET_LINES) {
            codesnippetLine = SnippetReplacer.applyReplacements(codesnippetLine, JAVADOC_CODESNIPPET_REPLACEMENTS);

            blackhole.consume(codesnippetLine);
        }
    }

    @Benchmark
    public void codesnippetReplacementPlayground(Blackhole blackhole) {
        for (String codesnippetLine : CODESNIPPET_LINES) {
            if (codesnippetLine.isEmpty()) {
                continue;
            }

            for (CodesnippetReplacementPlayground replacement : CODESNIPPET_PLAYGROUND_REPLACEMENT) {
                codesnippetLine = replacement.replaceCodesnippet(codesnippetLine);
            }

            blackhole.consume(codesnippetLine);
        }
    }


    static final class CodesnippetReplacementPlayground {
        private final String target;
        private final String replacement;
        private final Pattern optionalPattern;
        private final char targetChar;

        CodesnippetReplacementPlayground(String target, String replacement) {
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
}
