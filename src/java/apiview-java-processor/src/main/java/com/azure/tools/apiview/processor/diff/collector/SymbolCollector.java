package com.azure.tools.apiview.processor.diff.collector;

import com.github.javaparser.ast.body.CallableDeclaration;
import com.github.javaparser.ast.body.FieldDeclaration;
import com.github.javaparser.ast.body.TypeDeclaration;

/**
 * Collector interface used by JavaASTAnalyser to emit symbols for diff mode.
 */
public interface SymbolCollector {
    void onType(TypeDeclaration<?> typeDecl);
    void onField(TypeDeclaration<?> parentType, FieldDeclaration fieldDecl);
    void onMethod(TypeDeclaration<?> parentType, CallableDeclaration<?> callableDecl, boolean isConstructor);
}
