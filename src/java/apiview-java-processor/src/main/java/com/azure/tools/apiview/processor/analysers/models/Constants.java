package com.azure.tools.apiview.processor.analysers.models;

public class Constants {

    /************************************************************************************
     * Mode Flags
     ************************************************************************************/

    /** If true, we will output two files - a standard .json file, as well as a gzipped .json.tgz file. */
    public static final boolean GZIP_OUTPUT = true;

    /** Short names were a short-lived idea to reduce output size, before gzip support was added, kept here for posterity. */
    public static final boolean JSON_USE_SHORT_NAMES = false;

    /** Converts links within JavaDoc to hyperlinks, but can result in unintended links. */
    public static final boolean JAVADOC_EXTRACT_LINKS = false;


    /************************************************************************************
     * Properties
     ************************************************************************************/

    /** The filename of the icon to display for a TreeNode. */
    public static final String PROPERTY_ICON_NAME = "IconName";

    /**  */
    public static final String PROPERTY_SUBKIND = "SubKind";

    /**  */
    public static final String PROPERTY_URL_LINK_TEXT = "LinkText";

    /** Used to link to API elsewhere in the same review. */
    public static final String PROPERTY_NAVIGATE_TO_ID = "NavigateToId";

    /** The cross language definitionId for the node. */
    public static final String PROPERTY_CROSS_LANGUAGE_ID = "CrossLangDefId";

    public static final String PROPERTY_MODULE_NAME = "module-name";
    public static final String PROPERTY_MODULE_EXPORTS = "module-exports";
    public static final String PROPERTY_MODULE_REQUIRES = "module-requires";
    public static final String PROPERTY_MODULE_OPENS = "module-opens";


    /************************************************************************************
     * Tags
     ************************************************************************************/

    /**  */
    public static final String TAG_SKIP_DIFF = "SkipDiff";

    /**  */
    public static final String TAG_HIDE_FROM_NAVIGATION = "HideFromNav";


    /************************************************************************************
     * Render classes
     ************************************************************************************/

    // Render Classes are in the RenderClass enum


    /************************************************************************************
     * Annotation names
     ************************************************************************************/

    /** For @Deprecated annotation. */
    public static final String ANNOTATION_DEPRECATED = "Deprecated";

    public static final String ANNOTATION_SERVICE_METHOD = "ServiceMethod";
    public static final String ANNOTATION_SUPPRESS_WARNINGS = "SuppressWarnings";
    public static final String ANNOTATION_RETENTION = "Retention";
    public static final String ANNOTATION_TARGET = "Target";
    public static final String ANNOTATION_METADATA = "Metadata";


    /************************************************************************************
     * JSON output names
     ************************************************************************************/

    public static final String JSON_NAME_VERSION_STRING = JSON_USE_SHORT_NAMES ? "vs" : "VersionString";
    public static final String JSON_NAME_LANGUAGE = JSON_USE_SHORT_NAMES ? "l" : "Language";
    public static final String JSON_NAME_LANGUAGE_VARIANT = JSON_USE_SHORT_NAMES ? "lv" : "LanguageVariant";
    public static final String JSON_NAME_PACKAGE_NAME = JSON_USE_SHORT_NAMES ? "pn" : "PackageName";
    public static final String JSON_NAME_PACKAGE_VERSION = JSON_USE_SHORT_NAMES ? "pv" : "PackageVersion";
    public static final String JSON_NAME_API_FOREST = JSON_USE_SHORT_NAMES ? "af" : "APIForest";
    public static final String JSON_NAME_DIAGNOSTICS = JSON_USE_SHORT_NAMES ? "d" : "Diagnostics";
    public static final String JSON_NAME_NAME = JSON_USE_SHORT_NAMES ? "n" : "Name";
    public static final String JSON_NAME_ID = JSON_USE_SHORT_NAMES ? "i" : "Id";
    public static final String JSON_NAME_KIND = JSON_USE_SHORT_NAMES ? "k" : "Kind";
    public static final String JSON_NAME_TAGS = JSON_USE_SHORT_NAMES ? "t" : "Tags";
    public static final String JSON_NAME_PROPERTIES = JSON_USE_SHORT_NAMES ? "p" : "Properties";
    public static final String JSON_NAME_TOP_TOKENS = JSON_USE_SHORT_NAMES ? "tt" : "TopTokens";
    public static final String JSON_NAME_BOTTOM_TOKENS = JSON_USE_SHORT_NAMES ? "bt" : "BottomTokens";
    public static final String JSON_NAME_CHILDREN = JSON_USE_SHORT_NAMES ? "c" : "Children";
    public static final String JSON_NAME_VALUE = JSON_USE_SHORT_NAMES ? "v" : "Value";
    public static final String JSON_NAME_RENDER_CLASSES = JSON_USE_SHORT_NAMES ? "rc" : "RenderClasses";
}
