package com.azure.tools.apiview.processor.analysers

import com.azure.tools.apiview.processor.analysers.util.ASTUtils
import com.azure.tools.apiview.processor.analysers.util.TokenModifier
import com.azure.tools.apiview.processor.model.*
import com.jetbrains.rd.util.firstOrNull
import org.jetbrains.dokka.*
import org.jetbrains.dokka.base.generation.SingleModuleGeneration
import org.jetbrains.dokka.base.signatures.KotlinSignatureUtils.dri
import org.jetbrains.dokka.links.DRI
import org.jetbrains.dokka.model.*
import org.jetbrains.dokka.model.doc.*
import org.jetbrains.dokka.utilities.DokkaConsoleLogger
import org.jetbrains.dokka.utilities.LoggingLevel
import java.io.File

class KotlinASTAnalyser(private val apiListing: APIListing) {

    private var indent: Int = 0
    private val knownTypes = mutableMapOf<String, Documentable>()

    val BLOCKED_ANNOTATIONS: Set<String> = setOf("ServiceMethod", "SuppressWarnings")

    companion object {
        @JvmStatic
        fun hasPublicApiInKotlin(absolutePath: String) : Boolean {
            val packages = getPackages(absolutePath)

            return packages
                .flatMap { it.children }
                .filter { it is WithSources }
                .map { it as WithSources }
                .flatMap { it.sources.values }
                .any { it.path.endsWith(".kt") }
        }

        private fun getPackages(absolutePath: String): List<DPackage> {
            val files = setOf(File(absolutePath))

            val sourceSet = DokkaSourceSetImpl(
                sourceSetID = DokkaSourceSetID("DEFAULT", "DEFAULT"),
                sourceRoots = files,
            )

            val configuration = DokkaConfigurationImpl(
                sourceSets = listOf(sourceSet),
            )

            val logger = DokkaConsoleLogger(LoggingLevel.DEBUG)

            val dokkaGenerator = DokkaGenerator(configuration, logger)
            val context = dokkaGenerator.initializePlugins(configuration, logger)
            val singleModuleGeneration = context.single(CoreExtensions.generation) as SingleModuleGeneration
            val modulesFromPlatforms = singleModuleGeneration.createDocumentationModels()
            val filteredModules = singleModuleGeneration.transformDocumentationModelBeforeMerge(modulesFromPlatforms)
            val documentationModel = singleModuleGeneration.mergeDocumentationModels(filteredModules)

            return documentationModel!!.packages.filter { !it.packageName.contains("implementation") }
        }
    }

    fun analyse(absolutePath: String) {
        val packages = getPackages(absolutePath)
        scanForTypes(packages)

        packages.sortedBy { it.packageName }.forEach {
            processPackage(it)
        }
    }

    private fun scanForTypes(packages: List<DPackage>) {
        packages.forEach {
            it.children.forEach { documentable ->
                documentable.name?.let{ name ->
                    apiListing.addPackageTypeMapping(documentable.dri.packageName, name)
                    apiListing.knownTypes[name] = documentable.dri.packageName + "."+ name

                    knownTypes[name] = documentable
                }
            }
        }
    }

    private fun processPackage(dPackage: DPackage) {
        visitJavaDoc(dPackage.documentation)

        val packageName = dPackage.packageName

        addToken(Token(TokenKind.KEYWORD, "package"), TokenModifier.SPACE)

        val packageToken: Token
        if (packageName.isEmpty()) {
            packageToken = Token(TokenKind.TEXT, "<root package>")
        } else {
            packageToken = Token(TokenKind.TYPE_NAME, packageName, packageName)
            packageToken.navigateToId = packageName
        }
        addToken(packageToken, TokenModifier.SPACE)
        addToken(Token(TokenKind.PUNCTUATION, "{"), TokenModifier.NEWLINE)

        indent()

        dPackage.children.stream()
            .sorted(Comparator.comparing { s: Documentable -> s.name!! } )
            .forEach { documentable: Documentable ->

                when (documentable){
                    is DProperty -> tokeniseProperty(documentable)
                    is DFunction -> tokeniseConstructorsOrMethods(documentable)
                    else -> processClassOrInterfaceOrEnum(documentable)
                }
            }

        unindent()

        addToken(Token(TokenKind.PUNCTUATION, "}"), TokenModifier.NEWLINE)
    }

    private fun processClassOrInterfaceOrEnum(documentable: Documentable) {
        visitJavaDoc(documentable.documentation)

        getTypeDeclaration(documentable)

        // get fields
        tokeniseProperties(documentable)

        // get Constructors and methods
        if (documentable is DClass) {
            tokeniseConstructors(documentable.constructors)
            tokeniseCompanion(documentable.companion)
        }

        if (documentable is DAnnotation) {
            tokeniseConstructors(documentable.constructors)
            tokeniseCompanion(documentable.companion)
        }

        tokeniseFunctions(documentable)

        tokeniseInnerClasses(documentable.children)

        if (documentable is DInterface) {
            if (documentable.functions.isEmpty() && documentable.properties.isEmpty()) {
                // we have an empty interface declaration, it is probably a marker interface and we will leave a
                // comment to that effect
                indent()
                addComment("// This interface does not declare any API.")
                unindent()
            }
        }

        if (documentable is DEnum) {
            indent()
            documentable.entries
                .sortedBy { it.name }
                .forEach {
                    visitJavaDoc(it.documentation)

                    addToken(makeWhitespace())

                    // create a unique id for enum constants
                    val name: String = it.name
                    val definitionId: String = ASTUtils.makeId(it.dri.packageName + "." + it.dri.classNames )
                    addToken(Token(TokenKind.MEMBER_NAME, name, definitionId))

                    addToken(Token(TokenKind.PUNCTUATION, ","))
                    addNewLine()
                }
            unindent()
        }

        // close class
        addToken(makeWhitespace())
        addToken(Token(TokenKind.PUNCTUATION, "}"), TokenModifier.NEWLINE)
    }

    private fun tokeniseConstructors(constructors: List<DFunction>) {
        constructors.also {
            if (it.isEmpty()) {
                indent()
                addComment("// This class does not have any public constructors, and is not able to be instantiated using 'new'.")
                unindent()
            }
        }
            .sortedWith(compareBy { it.parameters.size })
            .forEach { constructor ->
                if (constructors.size == 1 && constructor.parameters.isEmpty())
                    return@forEach
                tokeniseConstructorsOrMethods(constructor)
            }
    }

    private fun tokeniseCompanion(companion: DObject?) {
        companion?.let{
            addToken(makeWhitespace())
            addToken(makeWhitespace())

            addToken(Token(TokenKind.KEYWORD, "companion"), TokenModifier.SPACE)
            addToken(Token(TokenKind.KEYWORD, "object"), TokenModifier.SPACE)

            addToken(Token(TokenKind.PUNCTUATION, "{"), TokenModifier.NEWLINE)

            indent()

            // get fields
            tokeniseProperties(it)
            tokeniseFunctions(it)

            unindent()
            addToken(makeWhitespace())
            addToken(makeWhitespace())
            addToken(Token(TokenKind.PUNCTUATION, "}"), TokenModifier.NEWLINE)

        }
    }


    private fun tokeniseFunctions(documentable: Documentable) {
        val functions = when (documentable) {
            is DClass -> documentable.functions
            is DObject -> documentable.functions
            is DInterface -> documentable.functions
            else -> return
        }

        functions
            .sortedWith(compareBy { it.name })
            .forEach { function ->
                tokeniseConstructorsOrMethods(function)
            }
    }

    private fun tokeniseConstructorsOrMethods(function: DFunction) {
        // Do not indent for functions declared directly in the package
        if (function.dri.classNames != null)
            indent()

        // print the JavaDoc above each method / constructor
        visitJavaDoc(function.documentation)
        // annotations
        addAnnotations(function, showAnnotationProperties = true, addNewline = true)

        addToken(makeWhitespace())

        // modifiers
        getModifiers(function)

        if (!function.isConstructor) {
            addToken(Token(TokenKind.KEYWORD, "fun"), TokenModifier.SPACE)
        }

        // Get type parameters
        getTypeParameters(function.generics)

        // if type parameters of method is not empty, we need to add a space before adding type name
        if (function.generics.isNotEmpty()) {
            addToken(Token(TokenKind.WHITESPACE, " "))
        }

        val isDeprecated = isDeprecated(function)
        if (isDeprecated) {
            addToken(Token(TokenKind.DEPRECATED_RANGE_START))
        }

        if (function.isConstructor) {
            val definitionId: String = function.dri.packageName + "." + function.dri.classNames + "." + function.name + function.parameters.size
            addToken(Token(TokenKind.TYPE_NAME,  "constructor", definitionId))
        }
        else {
            // extension and name
            if (function.receiver != null) {
                val it = function.receiver!!
                getType(it.type)
                addToken(Token(TokenKind.PUNCTUATION, "."))

                val functionName = "${getDRI(it.type).classNames}.${function.name}"
                val definitionId = ASTUtils.makeId("${function.dri.packageName}.$functionName")
                addToken(Token(TokenKind.MEMBER_NAME, function.name, definitionId))

                // Is Global function
                if (function.dri.classNames == null) {
                    val classNav = ChildItem(definitionId, functionName, TypeKind.FUNCTION)
                    apiListing.addChildItem(function.dri.packageName, classNav)
                }
            }
            else {
                // Is Global function
                if (function.dri.classNames == null) {
                    val definitionId = function.dri.packageName + "." + function.name
                    val classNav = ChildItem(definitionId, function.name, TypeKind.FUNCTION)
                    apiListing.addChildItem(function.dri.packageName, classNav)
                    addToken(Token(TokenKind.TYPE_NAME, function.name, definitionId))
                } else {
                    val definitionId = function.dri.packageName + "." + function.dri.classNames + "." + function.name
                    addToken(Token(TokenKind.TYPE_NAME, function.name, definitionId))
                }
            }
        }

        if (isDeprecated) {
            addToken(Token(TokenKind.DEPRECATED_RANGE_END))
        }

        addToken(Token(TokenKind.PUNCTUATION, "("))

        function.parameters
            .forEachIndexed { index, parameter ->

                // annotations
                addAnnotations(parameter, showAnnotationProperties = false, addNewline = false)

                addToken(Token(TokenKind.TEXT, parameter.name))
                addToken(Token(TokenKind.PUNCTUATION, ":"), TokenModifier.SPACE)
                getType(parameter.type)

                if (index < function.parameters.size - 1) {
                    addToken(Token(TokenKind.PUNCTUATION, ","), TokenModifier.SPACE)
                }
            }

        addToken(Token(TokenKind.PUNCTUATION, ")"))

        if (!function.isConstructor
            && function.type !is Void
            && !(function.type is GenericTypeConstructor && (function.type as GenericTypeConstructor).dri.classNames == "Unit")) {

            addToken(Token(TokenKind.PUNCTUATION, ":"), TokenModifier.SPACE)
            getType(function.type)
        }

        // close statements
        addNewLine()

        if (function.dri.classNames != null)
            unindent()
    }

    private fun tokeniseInnerClasses(children: List<Documentable>) {
        children
            .filter { it is DClass || it is DEnum || it is DInterface }
            .sortedBy { it.name }
            .forEach { documentable ->
                indent()
                processClassOrInterfaceOrEnum(documentable)
                unindent()
            }
    }

    private fun tokeniseProperties(documentable: Documentable) {
        val properties: List<DProperty> = when (documentable) {
            is DClass -> documentable.properties
            is DInterface -> documentable.properties
            is DObject -> documentable.properties
            is DAnnotation -> documentable.properties
            else -> return
        }

        indent()

        properties
            .forEach { property ->
                tokeniseProperty(property)
            }

        unindent()
    }

    private fun tokeniseProperty(property: DProperty) {

        visitJavaDoc(property.documentation)

        // annotations
        addAnnotations(property, showAnnotationProperties = true, addNewline = true)

        addToken(makeWhitespace())

        if (property.modifier.values.any { it.name == "open" })
            addToken(Token(TokenKind.KEYWORD, "open"), TokenModifier.SPACE)

        addToken(Token(TokenKind.KEYWORD, if (property.setter == null) "val" else "var"), TokenModifier.SPACE)

        val isDeprecated = isDeprecated(property)
        if (isDeprecated) {
            addToken(Token(TokenKind.DEPRECATED_RANGE_START))
        }

        // extension and name
        if (property.receiver != null) {
            val it = property.receiver!!
            getType(it.type)
            addToken(Token(TokenKind.PUNCTUATION, "."))

            val extensionName = "${getDRI(it.type).classNames}.${property.name}"
            val definitionId = ASTUtils.makeId("${property.dri.packageName}.$extensionName")

            val classNav = ChildItem(definitionId, extensionName, TypeKind.FUNCTION)
            apiListing.addChildItem(property.dri.packageName, classNav)
            addToken(Token(TokenKind.MEMBER_NAME, property.name, definitionId))
        }
        else {
            val definitionId: String = ASTUtils.makeId(property.dri.packageName + "." + property.dri.classNames + "." + property.name)
            addToken(Token(TokenKind.MEMBER_NAME, property.name, definitionId))
        }

        if (isDeprecated) {
            addToken(Token(TokenKind.DEPRECATED_RANGE_END))
        }

        addToken(Token(TokenKind.KEYWORD, ":"), TokenModifier.SPACE)

        // field type
        getType(property.type)


        // close the variable declaration
        addToken(Token(TokenKind.PUNCTUATION), TokenModifier.NEWLINE)
    }

    private fun indent() {
        indent += 4
    }

    private fun unindent() {
        indent = Math.max(indent - 4, 0)
    }

    private fun makeWhitespace(): Token {
        val sb = StringBuilder()
        for (i in 0 until indent) {
            sb.append(" ")
        }
        return Token(TokenKind.WHITESPACE, sb.toString())
    }

    private fun addComment(comment: String) {
        addToken(TokenModifier.INDENT, Token(TokenKind.COMMENT, comment), TokenModifier.NEWLINE)
    }
    private fun addNewLine() {
        addToken(Token(TokenKind.NEW_LINE))
    }

    private fun addToken(token: Token) {
        addToken(token, TokenModifier.NOTHING)
    }

    private fun addToken(token: Token, suffix: TokenModifier) {
        addToken(TokenModifier.NOTHING, token, suffix)
    }

    private fun addToken(prefix: TokenModifier, token: Token, suffix: TokenModifier) {
        handleTokenModifier(prefix)
        apiListing.tokens.add(token)
        handleTokenModifier(suffix)
    }

    private fun handleTokenModifier(modifier: TokenModifier) {
        when (modifier) {
            TokenModifier.INDENT -> addToken(makeWhitespace())
            TokenModifier.SPACE -> addToken(Token(TokenKind.WHITESPACE, " "))
            TokenModifier.NEWLINE -> addNewLine()
            TokenModifier.NOTHING -> {
            }
        }
    }

    private fun getModifiers(documentable: Documentable) {
        if (documentable is DClass){
            if (documentable.modifier.values.any { it.name == "open" }) {
                addToken(Token(TokenKind.KEYWORD, "open"), TokenModifier.SPACE)
            }
        }
    }

    private fun getTypeDeclaration(documentable: Documentable) {
        val typeKind: TypeKind = when (documentable) {
            is DClass -> TypeKind.CLASS
            is DInterface -> TypeKind.INTERFACE
            is DEnum -> TypeKind.ENUM
            is DObject -> TypeKind.OBJECT
            is DFunction -> TypeKind.FUNCTION
            is DProperty -> TypeKind.PROPERTY
            is DAnnotation -> TypeKind.ANNOTATION
            else -> return
        }

        // public class or interface or enum
        addAnnotations(documentable, showAnnotationProperties = true, addNewline = true)

        // Get modifiers
        addToken(makeWhitespace())
        getModifiers(documentable)

        // Create navigation for this class and add it to the parent
        val className = documentable.name
        val classId = ASTUtils.makeId("${documentable.dri.packageName}.$className")
        val classNav = ChildItem(classId, className, typeKind)
//            if (parentNav == null) {
            apiListing.addChildItem(documentable.dri.packageName, classNav)
//            } else {
//                parentNav.addChildItem(classNav)
//            }
//            parentNav = classNav

        addToken(Token(TokenKind.KEYWORD, typeKind.getName()), TokenModifier.SPACE)

        val isDeprecated =  isDeprecated(documentable)
        if (isDeprecated) {
            addToken(Token(TokenKind.DEPRECATED_RANGE_START))
        }

        addToken(Token(TokenKind.TYPE_NAME, className, classId))

        if (isDeprecated) {
            addToken(Token(TokenKind.DEPRECATED_RANGE_END))
        }

        // Type parameters of class definition
        if (documentable is DClass || documentable is DInterface) {

            val generics: List<DTypeParameter> = when (documentable) {
                is DClass -> documentable.generics
                is DInterface -> documentable.generics
                else -> throw IllegalArgumentException()
            }

            // Get type parameters
            getTypeParameters(generics)

            val supertypes: SourceSetDependent<List<TypeConstructorWithKind>> = when (documentable) {
                is DClass -> documentable.supertypes
                is DInterface -> documentable.supertypes
                else -> throw IllegalArgumentException()
            }
            if (supertypes.firstOrNull()?.value?.any() == true) {
                addToken(TokenModifier.SPACE, Token(TokenKind.KEYWORD, ":"), TokenModifier.SPACE)
            }

            var size: Int
            supertypes
                .flatMap { it.value }
                .map { it.typeConstructor }
                .also { size = it.size }
                .forEachIndexed { index, it ->
                    getType(it.dri, it.projections)

                    if (index < size - 1)
                        addToken(Token(TokenKind.PUNCTUATION, ","), TokenModifier.SPACE)
                }

        }

        // open ClassOrInterfaceDeclaration
        addToken(TokenModifier.SPACE, Token(TokenKind.PUNCTUATION, "{"), TokenModifier.NEWLINE)
    }

    private fun getTypeParameters(typeParameters: List<DTypeParameter>) {
        val size = typeParameters.size
        if (size == 0) {
            return
        }
        addToken(Token(TokenKind.PUNCTUATION, "<"))
        for (i in 0 until size) {
            val typeParameter = typeParameters[i]
            getGenericTypeParameter(typeParameter)
            if (i != size - 1) {
                addToken(Token(TokenKind.PUNCTUATION, ","), TokenModifier.SPACE)
            }
        }
        addToken(Token(TokenKind.PUNCTUATION, ">"))
    }

    private fun getGenericTypeParameter(typeParameter: DTypeParameter) {
        // set navigateToId
        val typeName = typeParameter.name
        val token = Token(TokenKind.TYPE_NAME, typeName)
        if (apiListing.knownTypes.containsKey(typeName)) {
            token.navigateToId = apiListing.knownTypes[typeName]
        }
        addToken(token)

        // get type bounds
        val typeBounds = typeParameter.bounds
        val size = typeBounds.size
        if (size != 0) {
            addToken(Token(TokenKind.KEYWORD, ":"), TokenModifier.SPACE)
            for (typeBound in typeBounds) {
                getType(typeBound)
            }
        }
    }

    private fun getDRI(typeBound: Bound) : DRI {
        return when (typeBound) {
            is GenericTypeConstructor -> {
                typeBound.dri
            }
            is Nullable -> {
                val genericTypeConstructor = typeBound.inner as GenericTypeConstructor
                return genericTypeConstructor.dri
            }
            is PrimitiveJavaType -> {
                typeBound.dri
            }
            else -> TODO()
        }
    }

    private fun getType(typeBound: Bound) {
        when (typeBound) {
            is GenericTypeConstructor -> {
                getType(typeBound.dri, typeBound.projections)
            }
            is Nullable -> {
                if (typeBound.inner is GenericTypeConstructor) {
                    val genericTypeConstructor = typeBound.inner as GenericTypeConstructor
                    getType(genericTypeConstructor.dri, genericTypeConstructor.projections)
                }
            }
            is PrimitiveJavaType -> {
                getType(typeBound.dri, listOf())
            }
            is TypeParameter -> {
                addToken(Token(TokenKind.TYPE_NAME, typeBound.name))
            }
            else -> TODO()
        }
    }

    private fun getType(dri: DRI, projection: List<Projection>) {
        getClassType(dri, projection)
    }

    private fun getClassType(dri: DRI, progection: List<Projection>) {
        this.getTypeDFS(dri, progection)
    }


    private fun getTypeDFS(dri: DRI, progection: List<Projection>) {
        val typeName = dri.classNames
        val token = Token(TokenKind.TYPE_NAME, typeName)
        if (apiListing.knownTypes.containsKey(typeName)) {
            token.navigateToId = apiListing.knownTypes[typeName]
        }
        addToken(token)

        progection.forEach {

            addToken(Token(TokenKind.PUNCTUATION, "<"))

            try {
                val type = (it as Invariance<TypeParameter>).inner
                getTypeDFS(type.dri, listOf())
            }
            catch (ex: Exception){ }

            try {
                val type = (it as Invariance<GenericTypeConstructor>).inner
                getTypeDFS(type.dri, type.projections)
            }
            catch (ex: Exception){ }

            addToken(Token(TokenKind.PUNCTUATION, ">"))
        }
    }

    private fun visitJavaDoc(set: SourceSetDependent<DocumentationNode>) {
        val tags = set.values
            .flatMap {
                it.children
            }

        if (tags.isEmpty())
            return

        addToken(Token(TokenKind.DOCUMENTATION_RANGE_START))
        addToken(makeWhitespace())
        addToken(Token(TokenKind.COMMENT, "/**"))

        tags.forEach { tagWrapper ->

                when (tagWrapper) {
                    is Description -> {
                        tagWrapper.children.forEach { docTag ->
                            addNewLine()
                            addToken(makeWhitespace())
                            addToken(Token(TokenKind.COMMENT, " * "))
                            build(docTag)
                        }
                    }
                    is Property -> {
                        tagWrapper.children.forEach { docTag ->
                            addNewLine()
                            addToken(makeWhitespace())
                            addToken(Token(TokenKind.COMMENT, " * "))
                            build(docTag)
                        }
                    }
                    is Param -> {
                        addNewLine()
                        addToken(makeWhitespace())
                        addToken(Token(TokenKind.COMMENT, " * "))
                        addNewLine()
                        addToken(makeWhitespace())
                        addToken(Token(TokenKind.COMMENT, " * @param ${tagWrapper.name} "))
                        tagWrapper.children.forEach { docTag ->
                            build(docTag)
                        }
                    }
                    is Sample -> {
                        addNewLine()
                        addToken(makeWhitespace())
                        addToken(Token(TokenKind.COMMENT, " * "))
                        addNewLine()
                        addToken(makeWhitespace())
                        addToken(Token(TokenKind.COMMENT, " * @sample ${tagWrapper.name} "))
                        tagWrapper.children.forEach { docTag ->
                            build(docTag)
                        }
                    }
                    else -> { }
                }
            }

        addNewLine()
        addToken(makeWhitespace())
        addToken(Token(TokenKind.COMMENT, " */"))
        addNewLine()
        addToken(Token(TokenKind.DOCUMENTATION_RANGE_END))
    }

    private fun build(tag: DocTag) {
        when (tag) {
            is Text -> {
                addToken(Token(TokenKind.COMMENT, tag.body))
                tag.children.forEach {
                    build(it)
                }
            }
            is I -> {
                addToken(Token(TokenKind.COMMENT, "*"))
                tag.children.forEach {
                    build(it)
                }
                addToken(Token(TokenKind.COMMENT, "*"))
            }
            is DocumentationLink -> {
                addToken(Token(TokenKind.COMMENT, "["))
                tag.children.forEach {
                    build(it)
                }
                addToken(Token(TokenKind.COMMENT, "]"))
            }
            else -> {
                tag.children.forEach {
                    build(it)
                }
            }
        }
    }

    private fun getAnnotations(documentable: Documentable): Collection<Annotations.Annotation>? {
        val directAnnotations = when (documentable) {
            is DClass -> documentable.extra[Annotations]?.directAnnotations
            is DInterface -> documentable.extra[Annotations]?.directAnnotations
            is DEnum -> documentable.extra[Annotations]?.directAnnotations
            is DObject -> documentable.extra[Annotations]?.directAnnotations
            is DFunction -> documentable.extra[Annotations]?.directAnnotations
            is DProperty -> documentable.extra[Annotations]?.directAnnotations
            is DParameter -> documentable.extra[Annotations]?.directAnnotations
            else -> null
        }

        return directAnnotations?.values?.flatten()
    }

    private fun isDeprecated(documentable: Documentable): Boolean {
        val annotations = getAnnotations(documentable)
        return annotations?.any { item -> item.dri.classNames == "Deprecated" } ?: false
    }

    private fun addAnnotations(documentable: Documentable, showAnnotationProperties: Boolean, addNewline: Boolean) {
        val annotations = getAnnotations(documentable)

        if (annotations == null || !annotations.any())
            return

        if (addNewline) {
            addToken(makeWhitespace())
        }

        annotations
            .filter {
                val id = it.dri.classNames
                !BLOCKED_ANNOTATIONS.contains(id) && id?.startsWith("Json") == false
            }
            .sortedBy { it.dri.classNames }
            .forEach {
                val token = Token(TokenKind.TYPE_NAME, "@" + it.dri.classNames)
                token.navigateToId = apiListing.knownTypes[it.dri.classNames]
                addToken(token)

                if (showAnnotationProperties) {
                    addToken(Token(TokenKind.PUNCTUATION, "("))

                    it.params.onEachIndexed { index, entry ->
                        addToken(Token(TokenKind.TEXT, entry.key))
                        addToken(Token(TokenKind.PUNCTUATION, " = "))
                        processAnnotationValueExpression(entry.value)

                        if (index < it.params.size - 1) {
                            addToken(Token(TokenKind.PUNCTUATION, ", "))
                        }
                    }

                    addToken(Token(TokenKind.PUNCTUATION, ")"))
                }

                if (addNewline) {
                    addNewLine()
                } else {
                    addToken(Token(TokenKind.WHITESPACE, " "))
                }
            }
    }

    private fun processAnnotationValueExpression(value: AnnotationParameterValue) {
        when (value) {
            is AnnotationValue -> {
                val token = Token(TokenKind.TYPE_NAME, value.annotation.dri.classNames)
                token.navigateToId = apiListing.knownTypes[value.annotation.dri.classNames]
                addToken(token)

                addToken(Token(TokenKind.PUNCTUATION, "("))
                value.annotation.params.onEachIndexed { index, entry ->

                    addToken(Token(TokenKind.TEXT, entry.key))
                    addToken(Token(TokenKind.PUNCTUATION, " = "))
                    processAnnotationValueExpression(entry.value)

                    if (index < value.annotation.params.size - 1) {
                        addToken(Token(TokenKind.PUNCTUATION, ", "))
                    }

                }
                addToken(Token(TokenKind.PUNCTUATION, ")"))

            }
            is ArrayValue -> {
                addToken(Token(TokenKind.PUNCTUATION, "["))
                value.value.onEachIndexed { index, annotationParameterValue ->
                    processAnnotationValueExpression(annotationParameterValue)
                    if (index < value.value.size - 1) {
                        addToken(Token(TokenKind.PUNCTUATION, ", "))
                    }
                }
                addToken(Token(TokenKind.PUNCTUATION, "]"))
            }
            is EnumValue -> {
                val token = Token(TokenKind.TYPE_NAME, value.enumName)
                value.enumDri.classNames?.let{
                    token.navigateToId = apiListing.knownTypes[it.split(".")[0]]
                }
                addToken(token)
            }
            is org.jetbrains.dokka.model.ClassValue -> {
                val token = Token(TokenKind.TYPE_NAME, value.className)
                token.navigateToId = apiListing.knownTypes[value.className]
                addToken(token)
            }

            is StringValue -> {
                addToken(Token(TokenKind.TEXT, "\"${value.value}\""))
            }
            is LiteralValue -> {
                addToken(Token(TokenKind.TEXT, value.text()))
            }
            else -> {
                addToken(Token(TokenKind.TEXT, value.toString()))
            }
        }
    }
}