package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.model.APIListing;
import com.github.javaparser.ast.CompilationUnit;

public interface DiagnosticRule {
    void scan(CompilationUnit cu, APIListing listing);
}
