package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.model.APIListing;

import java.nio.file.Path;
import java.util.List;

/**
 * We support multiple analysers, to serve different purposes.
 *
 * @see ASTAnalyser
 * @see ReflectiveAnalyser
 */
@FunctionalInterface
public interface Analyser {

    /**
     * Given the list of files, all of which came from a jar file (either a compiled class jar or a sources jar),
     * perform an analysis on it to populate the given APIListing model class.
     * This class will contain a list of tokens representing the entire public API surface area, as well as a navigation
     * hierarchy.
     *
     * @param allFiles A list of all files from the extracted jar file, some of which won't be relevant and can be
     *      ignored as necessary.
     * @param apiListing The model class to update that will be written out into JSON once this analysis completes.
     */
    void analyse(List<Path> allFiles, APIListing apiListing);
}
