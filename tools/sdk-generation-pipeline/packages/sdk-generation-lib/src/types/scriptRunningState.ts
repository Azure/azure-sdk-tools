const scriptRunningStateStrings = {
    /**
     * The process of running script has not yet begun.
     */
    pending: `Pending`,
    /**
     * TThe process of running script is in-progress.
     */
    inProgress: `In-Progress`,
    /**
     * TThe process of running script has failed.
     */
    failed: `Failed`,
    /**
     * TThe process of running script has succeeded.
     */
    succeeded: `Succeeded`,
    /**
     * TThe process of running script has warnings.
     */
    warning: `Warning`
};

export type scriptRunningState = keyof typeof scriptRunningStateStrings;
