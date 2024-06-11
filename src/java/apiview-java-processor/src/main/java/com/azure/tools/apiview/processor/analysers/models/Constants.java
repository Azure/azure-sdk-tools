package com.azure.tools.apiview.processor.analysers.models;

public class Constants {

    /************************************************************************************
     * Mode Flags
     ************************************************************************************/

    /** Short names were a short-lived idea to reduce output size, before gzip support was added, kept here for posterity. */
    public static final boolean JSON_USE_SHORT_NAMES = true;

    /** Creates a more succinct output - JavaDoc is grouped into separate values within the same token. */
    public static final boolean JAVADOC_COMBINE_INTO_SINGLE_TOKEN = false;

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

    public static final String JSON_NAME_NAME = JSON_USE_SHORT_NAMES ? "n" : "name";
    public static final String JSON_NAME_ID = JSON_USE_SHORT_NAMES ? "i" : "id";
    public static final String JSON_NAME_KIND = JSON_USE_SHORT_NAMES ? "k" : "kind";
    public static final String JSON_NAME_TAGS = JSON_USE_SHORT_NAMES ? "t" : "tags";
    public static final String JSON_NAME_PROPERTIES = JSON_USE_SHORT_NAMES ? "p" : "properties";
    public static final String JSON_NAME_TOP_TOKENS = JSON_USE_SHORT_NAMES ? "tt" : "topTokens";
    public static final String JSON_NAME_BOTTOM_TOKENS = JSON_USE_SHORT_NAMES ? "bt" : "bottomTokens";
    public static final String JSON_NAME_CHILDREN = JSON_USE_SHORT_NAMES ? "c" : "children";
    public static final String JSON_NAME_VALUE = JSON_USE_SHORT_NAMES ? "v" : "value";
    public static final String JSON_NAME_GROUP_VALUE = JSON_USE_SHORT_NAMES ? "gv" : "groupValue";
    public static final String JSON_NAME_RENDER_CLASSES = JSON_USE_SHORT_NAMES ? "rc" : "renderClasses";
}
