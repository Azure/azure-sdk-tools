package com.azure.tools.apiview.processor.analysers

import com.azure.tools.apiview.processor.analysers.util.ASTUtils
import com.azure.tools.apiview.processor.analysers.util.MiscUtils
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
import java.nio.file.Path
import java.util.*
import java.util.regex.Pattern

class KotlinASTAnalyser(private val apiListing: APIListing) : Analyser {

    private var indent: Int = 0
    private val knownTypes = mutableMapOf<String, Documentable>()

    override fun analyse(allFiles: List<Path?>?) {

    }

    override fun analyse(absolutePath: String) {

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
        val documentationModel = singleModuleGeneration.mergeDocumentationModels(filteredModules) ?: return

        val packages = documentationModel.packages

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
            .filter { it is DClass || it is DInterface || it is DEnum || it is DObject || it is DProperty || it is DFunction}
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
            documentable.constructors
                .also {
                    if (it.isEmpty()) {
                        indent()
                        addComment("// This class does not have any public constructors, and is not able to be instantiated using 'new'.")
                        unindent()
                    }
                }
                .sortedWith(compareBy { it.parameters.size })
                .forEach { constructor ->
                    if (documentable.constructors.size == 1 && constructor.parameters.isEmpty())
                        return@forEach
                    tokeniseConstructorsOrMethods(constructor)
                }

            documentable.companion?.let{
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
//                    visitJavaDoc(enumConstantDeclaration)

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
//        visitJavaDoc(callableDeclaration)
        addToken(makeWhitespace())

        // annotations
//        getAnnotations(callableDeclaration, false, false)

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

        val annotations = getAnnotations(function)
        val isDeprecated =  annotations?.any { list -> list.any { item -> item.dri.classNames == "Deprecated" } } == true
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
                    val classNav = ChildItem(definitionId, functionName, TypeKind.UNKNOWN)
                    apiListing.addChildItem(function.dri.packageName, classNav)
                }
            }
            else {
                // Is Global function
                if (function.dri.classNames == null) {
                    val definitionId = function.dri.packageName + "." + function.name
                    val classNav = ChildItem(definitionId, function.name, TypeKind.UNKNOWN)
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

//              visitJavaDoc(fieldDeclaration)

        addToken(makeWhitespace())

        // Add annotation for field declaration
        if (property.modifier.values.any { it.name == "open" })
            addToken(Token(TokenKind.KEYWORD, "open"), TokenModifier.SPACE)

        addToken(Token(TokenKind.KEYWORD, if (property.setter == null) "val" else "var"), TokenModifier.SPACE)

        // extension and name
        if (property.receiver != null) {
            val it = property.receiver!!
            getType(it.type)
            addToken(Token(TokenKind.PUNCTUATION, "."))

            val extensionName = "${getDRI(it.type).classNames}.${property.name}"
            val definitionId = ASTUtils.makeId("${property.dri.packageName}.$extensionName")

            val classNav = ChildItem(definitionId, extensionName, TypeKind.UNKNOWN)
            apiListing.addChildItem(property.dri.packageName, classNav)
            addToken(Token(TokenKind.MEMBER_NAME, property.name, definitionId))
        }
        else {
            val definitionId: String = ASTUtils.makeId(property.dri.packageName + "." + property.dri.classNames + "." + property.name)
            addToken(Token(TokenKind.MEMBER_NAME, property.name, definitionId))
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

    public fun makeWhitespace(): Token {
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

    public fun addToken(prefix: TokenModifier, token: Token, suffix: TokenModifier) {
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

        val visibilities: Collection<Visibility> = when (documentable) {
            is DClass -> documentable.visibility.values
            is DInterface -> documentable.visibility.values
            is DEnum -> documentable.visibility.values
            is DFunction -> documentable.visibility.values
            else -> return
        }

        if (documentable is DClass){
            if (documentable.modifier.values.any { it.name == "open" }) {
                addToken(Token(TokenKind.KEYWORD, "open"), TokenModifier.SPACE)
            }
        }

        // Do we need to add 'public' everywhere? If it is not public, it is not displayed at all
//        for (visibility in visibilities) {
//            addToken(Token(TokenKind.KEYWORD, visibility.name), TokenModifier.SPACE)
//        }
    }

    private fun getAnnotations(documentable: Documentable): Collection<List<Annotations.Annotation>>? {
        return when (documentable) {
            is DClass -> documentable.extra[Annotations]?.directAnnotations?.values
            is DInterface -> documentable.extra[Annotations]?.directAnnotations?.values
            is DEnum -> documentable.extra[Annotations]?.directAnnotations?.values
            is DFunction -> documentable.extra[Annotations]?.directAnnotations?.values
            else -> null
        }
    }

    private fun getTypeDeclaration(documentable: Documentable) {
        val typeKind: TypeKind = when (documentable) {
            is DClass -> TypeKind.CLASS
            is DInterface -> TypeKind.INTERFACE
            is DEnum -> TypeKind.ENUM
            is DObject -> TypeKind.OBJECT
            else -> return
        }

        // public class or interface or enum
//            getAnnotations(typeDeclaration, true, true)

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

        val annotations = getAnnotations(documentable)
        val isDeprecated =  annotations?.any { list -> list.any { item -> item.dri.classNames == "Deprecated" } } == true
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
//                    is TypeParameter -> TODO()
//                    is FunctionalTypeConstructor -> TODO()
//                    is TypeAliased -> TODO()
//                    is PrimitiveJavaType -> TODO()
//                    Void -> TODO()
//                    is JavaObject -> TODO()
//                    Dynamic -> TODO()
//                    is UnresolvedBound -> TODO()
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
//                    is TypeParameter -> TODO()
//                    is FunctionalTypeConstructor -> TODO()
//                    is TypeAliased -> TODO()
//                    is PrimitiveJavaType -> TODO()
//                    Void -> TODO()
//                    is JavaObject -> TODO()
//                    Dynamic -> TODO()
//                    is UnresolvedBound -> TODO()
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
            catch (ex: Exception){
            }

            try {
                val type = (it as Invariance<GenericTypeConstructor>).inner
                getTypeDFS(type.dri, type.projections)
            }
            catch (ex: Exception){
            }


            addToken(Token(TokenKind.PUNCTUATION, ">"))
        }


    }
    private val SPLIT_NEWLINE = Pattern.compile(MiscUtils.LINEBREAK)

    private fun visitJavaDoc(set: SourceSetDependent<org.jetbrains.dokka.model.doc.DocumentationNode>) {


        addToken(Token(TokenKind.DOCUMENTATION_RANGE_START))
        set.values
            .flatMap {
                it.children
            }
            .forEach {
                when (it) {
                    is Description -> {
                        build(it.root)
                        it.children.forEach { build(it) }

                    }
                    is Sample -> {

                    }
                    is Param -> {

                    }
                    is Constructor -> {

                    }
                    else -> { }
                }
            }
//        Arrays.stream(SPLIT_NEWLINE.split(jd.toString())).forEach { line: String? ->
//            // we want to wrap our javadocs so that they are easier to read, so we wrap at 120 chars
//            val wrappedString = MiscUtils.wrap(line, 120)
//            Arrays.stream(SPLIT_NEWLINE.split(wrappedString))
//                .forEach { line2: String? ->
//                    addToken(makeWhitespace())
//                    addToken(
//                        Token(
//                            TokenKind.COMMENT,
//                            line2
//                        )
//                    )
//                    addNewLine()
//                }
//        }
        addToken(Token(TokenKind.DOCUMENTATION_RANGE_END))
    }

    private fun build(tag: DocTag) {
        tag.children.forEach { p ->

            when (p) {
                is Text -> {
                    addToken(Token(TokenKind.COMMENT, p.body))
                    p.children.forEach {
                        build(it)
                    }
                }
                is org.jetbrains.dokka.model.doc.I -> {
                    addToken(Token(TokenKind.COMMENT, "*"))
                    p.children.forEach {
                        build(it)
                    }
                    addToken(Token(TokenKind.COMMENT, "*"))
                }
            }
        }
    }
}