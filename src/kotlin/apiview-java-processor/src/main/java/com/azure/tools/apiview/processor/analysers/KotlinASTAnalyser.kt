package com.azure.tools.apiview.processor.analysers

import com.github.javaparser.utils.Utils
import kotlinx.ast.common.AstSource
import kotlinx.ast.common.ast.Ast
import kotlinx.ast.common.print
import kotlinx.ast.grammar.kotlin.common.summary
import kotlinx.ast.grammar.kotlin.target.antlr.kotlin.KotlinGrammarAntlrKotlinParser
import java.io.BufferedReader
import java.io.IOException
import java.io.InputStream
import java.io.InputStreamReader
import java.nio.file.Files
import java.nio.file.Path

class KotlinASTAnalyser : Analyser {
    override fun analyse(allFiles: List<Path>) {

        fun processPackage(unit: Unit?) {

        }
        allFiles.stream()
            .filter(this::filterFilePaths)
            .map(this::scanForTypes)
            .forEach(::processPackage)

    }

    private fun scanForTypes(path: Path) {

        try {
            val inputStream = Files.newInputStream(Utils.assertNotNull(path) as Path)
            val fileContent = readFromInputStream(inputStream)

            val source = AstSource.String(
                description = "descrptn",
                content = fileContent
            )

            val kotlinFile = KotlinGrammarAntlrKotlinParser.parseKotlinFile(source)

            kotlinFile.summary(attachRawAst = false)
                .onSuccess { astList ->
                    astList.forEach(Ast::print)
                }.onFailure { errors ->
                    errors.forEach(::println)
                }
        }
        catch (ex: Throwable) {
            val error = ex.message
            val stack = ex.stackTrace
        }


    }

    @Throws(IOException::class)
    private fun readFromInputStream(inputStream: InputStream): String {
        val resultStringBuilder = StringBuilder()
        BufferedReader(InputStreamReader(inputStream)).use { br ->
            var line = br.readLine()
            while (line != null) {
                resultStringBuilder.append(line).append("\n")
                line = br.readLine()
            }
        }
        return resultStringBuilder.toString()
    }

    private fun filterFilePaths(filePath: Path): Boolean {
        val fileName = filePath.toString()
        // Skip paths that are directories.
        return if (Files.isDirectory(filePath)) {
            false
        } else {
            // Only include Kotlin files.
            fileName.endsWith(".kt")
        }
    }

}