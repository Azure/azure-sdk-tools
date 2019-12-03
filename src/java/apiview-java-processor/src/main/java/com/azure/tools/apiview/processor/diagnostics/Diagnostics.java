package com.azure.tools.apiview.processor.diagnostics;

import com.azure.tools.apiview.processor.diagnostics.rules.BadPrefixesDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.IllegalPackageAPIExportsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.ImportsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.NoPublicFieldsDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.PackageNameDiagnosticRule;
import com.azure.tools.apiview.processor.diagnostics.rules.UpperCaseNamingDiagnosticRule;
import com.azure.tools.apiview.processor.model.APIListing;
import com.github.javaparser.ast.CompilationUnit;

import java.util.ArrayList;
import java.util.List;

public class Diagnostics {
    private static final List<DiagnosticRule> diagnostics = new ArrayList<>();
    static {
        diagnostics.add(new PackageNameDiagnosticRule());
        diagnostics.add(new ImportsDiagnosticRule("com.sun"));
        diagnostics.add(new IllegalPackageAPIExportsDiagnosticRule("implementation", "netty"));
        diagnostics.add(new NoPublicFieldsDiagnosticRule());
        diagnostics.add(new UpperCaseNamingDiagnosticRule("URL", "HTTP", "XML", "JSON", "SAS", "CPK", "API"));
        diagnostics.add(new BadPrefixesDiagnosticRule("isHas", "setHas"));
    }

    public static void scan(CompilationUnit cu, APIListing listing) {
        for (DiagnosticRule rule : diagnostics) {
            rule.scan(cu, listing);
        }
    }
}
