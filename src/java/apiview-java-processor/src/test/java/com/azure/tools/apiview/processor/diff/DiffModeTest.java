package com.azure.tools.apiview.processor.diff;

import com.azure.tools.apiview.processor.Main;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.*;

/**
 * Integration-style tests for the --diff mode of the apiview-java-processor.
 */
public class DiffModeTest {

    @Test
    public void testAddedAndRemovedAndModifiedMethod() throws Exception {
        Path tempDir = Files.createTempDirectory("apiview-diff-test");
        Path oldSrc = Files.createDirectory(tempDir.resolve("old"));
        Path newSrc = Files.createDirectory(tempDir.resolve("new"));
        Path outDir = Files.createDirectory(tempDir.resolve("out"));

        // Old version: one class with one method foo(int a)
        String oldClass = "package test;\n" +
                "public class Sample {\n" +
                "  public int foo(int a) { return a; }\n" +
                "  protected String keep(String x) { return x; }\n" +
                "}\n";
        // New version: foo(int a) return type changed to long, added bar(), parameter name changed in keep method
        String newClass = "package test;\n" +
                "public class Sample {\n" +
                "  public long foo(int a) { return a; }\n" +
                "  protected String keep(String renamed) { return renamed; }\n" +
                "  public void bar() {}\n" +
                "}\n";

        Path oldFile = oldSrc.resolve("Sample.java");
        Path newFile = newSrc.resolve("Sample.java");
        Files.write(oldFile, oldClass.getBytes(StandardCharsets.UTF_8));
        Files.write(newFile, newClass.getBytes(StandardCharsets.UTF_8));

        // Run diff mode
        String[] args = new String[] {"--diff", "--old", oldSrc.toString(), "--new", newSrc.toString(), "--out", outDir.toString()};
        Main.main(args);

        // Read output file apiview-diff.json
        Path diffJson = outDir.resolve("apiview-diff.json");
        assertTrue(Files.exists(diffJson), "Diff output file should exist");
        String json = Files.readAllLines(diffJson, StandardCharsets.UTF_8).stream().collect(Collectors.joining());

        // Basic assertions on change types.
        assertTrue(json.contains("AddedMethod"), "Should contain AddedMethod change for bar()");
        assertTrue(json.contains("ModifiedMethodReturnType"), "Should contain ModifiedMethodReturnType for foo");
        assertTrue(json.contains("ModifiedMethodParameterNames"), "Should contain parameter name change for keep method");
        // No removal expected
        assertFalse(json.contains("RemovedMethod"), "No methods removed in this scenario");

        // Sanity check: ensure signature appears
        assertTrue(json.contains("Sample#foo(int)"), "Signature for foo should appear");
    }

    @Test
    public void testFieldAndClassChanges() throws Exception {
        Path tempDir = Files.createTempDirectory("apiview-diff-test2");
        Path oldSrc = Files.createDirectory(tempDir.resolve("old"));
        Path newSrc = Files.createDirectory(tempDir.resolve("new"));
        Path outDir = Files.createDirectory(tempDir.resolve("out"));

        String oldClass = "package test;\n" +
                "public class Holder {\n" +
                "  public int value;\n" +
                "}\n";
        String newClass = "package test;\n" +
                "public class Holder {\n" +
                "  public long value; // type changed\n" +
                "  public int extra; // added field\n" +
                "}\n";

        Files.write(oldSrc.resolve("Holder.java"), oldClass.getBytes(StandardCharsets.UTF_8));
        Files.write(newSrc.resolve("Holder.java"), newClass.getBytes(StandardCharsets.UTF_8));

        Main.main(new String[]{"--diff", "--old", oldSrc.toString(), "--new", newSrc.toString(), "--out", outDir.toString()});

        Path diffJson = outDir.resolve("apiview-diff.json");
        assertTrue(Files.exists(diffJson), "Diff output file should exist");
        String json = Files.readAllLines(diffJson, StandardCharsets.UTF_8).stream().collect(Collectors.joining());

        assertTrue(json.contains("ModifiedFieldType"), "Should detect field type change");
        assertTrue(json.contains("AddedField"), "Should detect added field");
        assertFalse(json.contains("RemovedField"), "No field removal here");
        assertFalse(json.contains("RemovedClass"), "Class not removed");
    }
}
