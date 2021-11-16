package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.model.APIListing;
import com.github.javaparser.ast.CompilationUnit;

public interface DiagnosticRule {
    void scanIndividual(CompilationUnit cu, APIListing listing);

    default void scanFinal(APIListing listing) {
        // no-op (not all diagnostics care about doing a final scan)
    }
}
