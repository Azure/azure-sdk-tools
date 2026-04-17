namespace ApiView;

/** C#: UserProfileModel */
model UserProfile {
    /** Github username */
    id: string;
    Email: string;
    Preferences: UserPreferences;
}

/** C#: PreferencesModel */
model UserPreferences {
    /** Github username for the user. */
    userName: string;
    /** No idea what this does? Seems like legacy. */
    language: string[];
    /** The UI theme */
    theme: string;
    /** Toggle the display of the options panel on the review page on or off. */
    hideReviewPageOptions: boolean;
    /** Toggle the display of the options panel on the samples page on or off. */
    hideSamplesPageOptions: boolean;
    /** Toggle the display of line numbers on or off. */
    hideLineNumbers: boolean;
    /** Toggle the display of the left navigation pane on or off. */
    hideLeftNavigation: boolean;
    /** Toggle the display of hidden APIs on or off. */
    showHiddenApis: boolean;
    /** Toggle the display of APIView documentation on or off (for languages that support documentation). */
    showDocumentation: boolean;
    /** Toggle the display of APIView comments on or off. */
    showComments: boolean;
    /** Toggle the display of APIView diagnostics on or off. */
    showSystemComments: boolean;
    /** Replace the default home page with the experimental split-index homepage. */
    useBetaIndexPage: boolean;
    /** Disables lazy loading of code lines. */
    disableCodeLinesLazyLoading: boolean;
}
