package com.azure.tools.apiview.processor.diff.model;

import java.util.HashMap;
import java.util.Map;

/**
 * Root container for extracted public/protected API symbols.
 */
public final class ApiSymbolTable {
    /** Class symbols keyed by fully-qualified class name. */
    public final Map<String, ClassSymbol> classes = new HashMap<>();
}
