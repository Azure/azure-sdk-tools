package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ChildItem;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.TypeKind;

import java.io.File;
import java.lang.reflect.Constructor;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Parameter;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.lang.reflect.TypeVariable;
import java.net.MalformedURLException;
import java.net.URL;
import java.net.URLClassLoader;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import static java.lang.reflect.Modifier.isAbstract;
import static java.lang.reflect.Modifier.isFinal;
import static java.lang.reflect.Modifier.isProtected;
import static java.lang.reflect.Modifier.isPublic;
import static java.lang.reflect.Modifier.isStatic;
import static com.azure.tools.apiview.processor.model.TokenKind.KEYWORD;
import static com.azure.tools.apiview.processor.model.TokenKind.MEMBER_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.NEW_LINE;
import static com.azure.tools.apiview.processor.model.TokenKind.PUNCTUATION;
import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;
import static com.azure.tools.apiview.processor.model.TokenKind.TYPE_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.WHITESPACE;

public class ReflectiveAnalyser implements Analyser {
    private int indent = 0;

    // maps from a class simple name to the id generated with makeId(Class)
    private final Map<String, String> knownTypes;

    private final File tempDir;

    public ReflectiveAnalyser(File tempDir) {
        this.knownTypes = new HashMap<>();
        this.tempDir = tempDir;
    }

    @Override
    public void analyse(List<Path> allFiles, APIListing apiListing) {
        // we build a custom classloader so that we can load classes that were not on the classpath
        String rootDirectory = tempDir.getPath();

        ClassLoader classLoader = null;
        try {
            URL url = new File(rootDirectory).toURI().toURL();
            URL[] urls = new URL[] {url};
            classLoader = URLClassLoader.newInstance(urls);
        } catch (MalformedURLException e) {
            e.printStackTrace();
            System.exit(-1);
        }
        final ClassLoader cl = classLoader;

        // firstly we filter out the files we don't care about
        allFiles = allFiles.stream()
                .filter(path -> {
                    File inputFile = path.toFile();
                    String inputFileName = inputFile.toString();
                    if (inputFile.isDirectory()) return false;
                    else if (inputFileName.contains("implementation")) return false;
                    else if (!inputFileName.endsWith(".class")) return false;
                    else return true;
                }).collect(Collectors.toList());

        // then we do a pass to build a map of all known types,
        // followed by a pass to tokenise each file
        allFiles.stream()
                .map(path -> scanForTypes(path, cl))
                .collect(Collectors.toList())
                .stream()
                .filter(Optional::isPresent)
                .map(Optional::get)
                .forEach(scanClass -> processSingleFile(scanClass, apiListing));
    }

    private static class ScanClass {
        private Class<?> cls;
        private Path path;

        public ScanClass(Path path, Class<?> cls) {
            this.cls = cls;
            this.path = path;
        }
    }

    private Optional<ScanClass> scanForTypes(Path path, ClassLoader classLoader) {
        File inputFile = path.toFile();
        String inputFileName = inputFile.toString();

        // The input file will look like 'temp/azure-core-1.0.0-preview.4.jar/com/azure/core/exception/ServiceResponseException.class',
        // we want this to be 'com/azure/core/exception/ServiceResponseException.class',
        // which then can become 'com.azure.core.exception.ServiceResponseException'
        final String fqcn = inputFileName
                .substring(inputFileName.indexOf(".jar/") + 5, inputFileName.length() - 6)
                .replaceAll("/", ".");

        try {
            Class cls = classLoader.loadClass(fqcn);
            return scanForTypes(cls) ? Optional.of(new ScanClass(path, cls)) : Optional.empty();
        } catch (ClassNotFoundException e) {
            e.printStackTrace();
        }

        return Optional.empty();
    }

    private boolean scanForTypes(Class<?> cls) {
        if (! (isPublic(cls.getModifiers()) || isProtected(cls.getModifiers()))) {
            return false;
        }

        knownTypes.put(cls.getSimpleName(), makeId(cls));

        Stream.of(cls.getDeclaredClasses()).forEach(this::scanForTypes);
        return true;
    }

    private void processSingleFile(ScanClass scanClass, APIListing apiListing) {
        File inputFile = scanClass.path.toFile();
        String inputFileName = inputFile.toString();

        // Root Navigation
        ChildItem rootNavForJar = new ChildItem(inputFile.getName(), TypeKind.ASSEMBLY);
        apiListing.addChildItem(rootNavForJar);

        getClassAPI(scanClass.cls, apiListing, rootNavForJar);
    }

    private boolean getClassAPI(Class<?> cls, APIListing apiListing, ChildItem parent) {
        final List<Token> tokens = apiListing.getTokens();

        // class modifier
        boolean isPublicClass = getModifiers(cls.getModifiers(), tokens);
        if (!isPublicClass) {
            return false;
        }

        final String className = cls.getSimpleName();
        final String classId = makeId(cls);

        // Create navigation for this class and add it to the parent
        ChildItem classNav = new ChildItem(classId, cls.getSimpleName(), TypeKind.fromClass(cls));
        parent.addChildItem(classNav);

        // class name
        tokens.add(new Token(KEYWORD, "class"));
        tokens.add(new Token(WHITESPACE, " "));
        tokens.add(new Token(TYPE_NAME, className, classId));
        tokens.add(new Token(WHITESPACE, " "));
        tokens.add(new Token(PUNCTUATION, "{"));
        tokens.add(new Token(NEW_LINE, ""));

        indent();

        // fields
        Stream.of(cls.getDeclaredFields())
                .sorted(Comparator.comparing(Field::getName))
                .forEach(field ->  {
                    // modifiers
                    boolean isPublicAPI = getModifiers(field.getModifiers(), tokens);
                    if (!isPublicAPI) {
                        return;
                    }

                    // field type
                    getType(field.getGenericType(), tokens);
                    tokens.add(new Token(WHITESPACE, " "));

                    // field name
                    tokens.add(new Token(MEMBER_NAME, field.getName()));

                    tokens.add(new Token(PUNCTUATION, ";"));
                    tokens.add(new Token(NEW_LINE, ""));
                });

        // constructors
        Stream.of(cls.getDeclaredConstructors())
                .sorted(Comparator.comparing(Constructor::getName))
                .forEach(constructor ->  {
                    // modifiers
                    boolean isPublicAPI = getModifiers(constructor.getModifiers(), tokens);
                    if (!isPublicAPI) {
                        return;
                    }

                    // constructor name
                    String name = constructor.getDeclaringClass().getSimpleName();
                    String definitionId = constructor.toString().replaceAll(" ", "-");
                    tokens.add(new Token(MEMBER_NAME, name, definitionId));

                    // opening brace
                    tokens.add(new Token(PUNCTUATION, "("));

                    // parameters
                    getParameters(constructor.getParameters(), tokens);

                    // closing brace and new line
                    tokens.add(new Token(PUNCTUATION, ")"));
                    tokens.add(new Token(WHITESPACE, " "));
                    tokens.add(new Token(PUNCTUATION, "{"));
                    tokens.add(new Token(WHITESPACE, " "));
                    tokens.add(new Token(PUNCTUATION, "}"));
                    tokens.add(new Token(NEW_LINE, ""));
                });

        // methods
        Stream.of(cls.getDeclaredMethods())
                .sorted(Comparator.comparing(Method::getName))
                .forEach(method -> {
                    // modifiers
                    boolean isPublicAPI = getModifiers(method.getModifiers(), tokens);
                    if (!isPublicAPI) {
                        return;
                    }

                    // return type
                    getType(method.getGenericReturnType(), tokens);
                    tokens.add(new Token(WHITESPACE, " "));

                    // method name
                    String definitionId = method.toString().replaceAll(" ", "-");
                    tokens.add(new Token(MEMBER_NAME, method.getName(), definitionId));

                    // opening brace
                    tokens.add(new Token(PUNCTUATION, "("));

                    // parameters
                    getParameters(method.getParameters(), tokens);

                    // closing brace and new line
                    tokens.add(new Token(PUNCTUATION, ")"));
                    tokens.add(new Token(WHITESPACE, " "));
                    tokens.add(new Token(PUNCTUATION, "{"));
                    tokens.add(new Token(WHITESPACE, " "));
                    tokens.add(new Token(PUNCTUATION, "}"));
                    tokens.add(new Token(NEW_LINE, ""));
                });

        // handle enclosed classes, passing in child navigation as we go deeper
        Stream.of(cls.getClasses())
                .forEach(subclass -> getClassAPI(subclass, apiListing, classNav));

        // close class
        tokens.add(new Token(PUNCTUATION, "}"));
        tokens.add(new Token(NEW_LINE, ""));

        unindent();

        return true;
    }

    private boolean getModifiers(int modifiers, List<Token> tokens) {
        // abort - we only care about public and protected methods
        if (! (isPublic(modifiers) || isProtected(modifiers))) {
            return false;
        }

        // indentation
        tokens.add(makeWhitespace());

        if (isPublic(modifiers)) {
            tokens.add(new Token(KEYWORD, "public"));
        } else if (isProtected(modifiers)) {
            tokens.add(new Token(KEYWORD, "protected"));
        }

        tokens.add(new Token(WHITESPACE, " "));

        if (isAbstract(modifiers)) {
            tokens.add(new Token(KEYWORD, "abstract"));
            tokens.add(new Token(WHITESPACE, " "));
        }
        if (isFinal(modifiers)) {
            tokens.add(new Token(KEYWORD, "final"));
            tokens.add(new Token(WHITESPACE, " "));
        }
        if (isStatic(modifiers)) {
            tokens.add(new Token(KEYWORD, "static"));
            tokens.add(new Token(WHITESPACE, " "));
        }

        return true;
    }

    private void getParameters(Parameter[] parameters, List<Token> tokens) {
        for(int i = 0; i < parameters.length; i++) {
            Parameter parameter = parameters[i];
            getType(parameter.getParameterizedType(), tokens);
            tokens.add(new Token(WHITESPACE, " "));
            tokens.add(new Token(TEXT, parameter.getName()));

            // add comma and space until the last parameter
            if (i < parameters.length - 1) {
                tokens.add(new Token(PUNCTUATION, ","));
                tokens.add(new Token(WHITESPACE, " "));
            }
        }
    }

    private void getType(Type type, List<Token> tokens) {
        if (type instanceof ParameterizedType) {
            ParameterizedType parameterizedType = (ParameterizedType) type;
            Type[] parameterTypes = parameterizedType.getActualTypeArguments();

            Class<?> rawType = (Class<?>) parameterizedType.getRawType();
            getType(rawType, tokens);
            tokens.add(new Token(PUNCTUATION, "<"));

            for(int i = 0; i < parameterTypes.length; i++) {
                getType(parameterTypes[i], tokens);

                // add comma and space until the last parameter
                if (i < parameterTypes.length - 1) {
                    tokens.add(new Token(PUNCTUATION, ","));
                    tokens.add(new Token(WHITESPACE, " "));
                }
            }

            tokens.add(new Token(PUNCTUATION, ">"));
        } else if (type instanceof Class) {
            getClassType((Class<?>)type, tokens);
        } else if (type instanceof TypeVariable) {
            tokens.add(new Token(TYPE_NAME, ((TypeVariable<?>) type).getName()));
        } else {
            System.err.println("Unknown type " + type + " of type " + type.getClass());
        }
    }

    private void getClassType(Class<?> type, List<Token> tokens) {
        if (type.isArray()) {
            getClassType(type.getComponentType(), tokens);
            tokens.add(new Token(PUNCTUATION, "[]"));
        } else {
            String typeName = type.getSimpleName();
            Token token = new Token(TYPE_NAME, typeName);
            if (knownTypes.containsKey(typeName)) {
                token.setNavigateToId(knownTypes.get(typeName));
            }
            tokens.add(token);
        }
    }

    private void indent() {
        indent += 4;
    }

    private void unindent() {
        indent = Math.max(indent - 4, 0);
    }

    private Token makeWhitespace() {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < indent; i++) {
            sb.append(" ");
        }
        return new Token(WHITESPACE, sb.toString());
    }

    private String makeId(Class<?> cls) {
        return cls.getCanonicalName().replaceAll(" ", "-");
    }
}
