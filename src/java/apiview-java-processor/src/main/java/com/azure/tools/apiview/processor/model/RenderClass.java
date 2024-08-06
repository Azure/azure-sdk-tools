package com.azure.tools.apiview.processor.model;

public enum RenderClass {
    PUNCTUATION("punctuation"),
    TYPE_NAME("typeName"),
    KEYWORD("keyword"),
    STRING_LITERAL("stringLiteral"),
    NUMBER("number"),
    PACKAGE_NAME("packageName"),
    MODULE_NAME("moduleName"),
    ENUM_TYPE("enumType"),
    ENUM_CONSTANT("enumConstant"),
    ANNOTATION_NAME("annotationName"),
    ANNOTATION_PARAMETER_NAME("annotationParameterName"),
    ANNOTATION_PARAMETER_VALUE("annotationParameterValue"),
    RETURN_TYPE("returnType"),
    PARAMETER_TYPE("parameterType"),
    PARAMETER_NAME("parameterName"),
    EXTENDS_TYPE("extendsType"),
    IMPLEMENTS_TYPE("implementsType"),
    MEMBER_NAME("memberName"),
    METHOD_NAME("methodName"),
    FIELD_NAME("fieldName"),
    MAVEN_KEY("mavenKey"),
    MAVEN_VALUE("mavenValue"),
    MAVEN_DEPENDENCY("dependency"),
    COMMENT("comment"),
    JAVADOC("javadoc"),
    DEPRECATED("deprecated");

    private final String value;

    RenderClass(String value) {
        this.value = value;
    }

    public String getValue() {
        return value;
    }
}
