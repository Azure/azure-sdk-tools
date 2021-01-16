$(() => {  
    const SEL_DOC_CLASS = ".documentation";
    const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
    const SHOW_DOC_CHECKBOX = ".show-doc-checkbox";
    const SHOW_DOC_HREF = ".show-document";
    const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
    const SHOW_DIFFONLY_HREF = ".show-diffonly";

    hideCheckboxIfNoDocs();

    function hideCheckboxIfNoDocs() {
        if ($(SEL_DOC_CLASS).length == 0) {
            $(SHOW_DOC_CHECK_COMPONENT).hide();
        }
    }

    $(SHOW_DOC_CHECKBOX).on("click", e => {
        $(SHOW_DOC_HREF)[0].click();
    });

    $(SHOW_DIFFONLY_CHECKBOX).on("click", e => {
        $(SHOW_DIFFONLY_HREF)[0].click();
    });

    // Add a mode for keyboard navigation between diff blocks.  This is an
    // optional feature that's disabled by default.  If there are changed lines
    // of code, pressing `Alt+j` will enable it, `j` will move to the next
    // block, and `k` will move to the previous block.
    {
        const DIFF_NAV_ENABLE_KEY_MOD = "Alt";
        const DIFF_NAV_ENABLE_KEY = "j";
        const DIFF_NAV_NEXT_KEY = "j";
        const DIFF_NAV_PREV_KEY = "k";

        const DIFF_NAV_LINE_SELECTOR = ".code";
        const DIFF_NAV_ADDED_CLASS = "code-added";
        const DIFF_NAV_REMOVED_CLASS = "code-removed";
        const DIFF_NAV_FOCUSED_CLASS = "code-diff-focused";

        // Walk through all the lines of code to find the first line of each
        // "block" of contiguous added/removed lines
        const diffNavBlocks: HTMLElement[] = [];
        const codeLines = $(DIFF_NAV_LINE_SELECTOR);
        if (codeLines.length > 0) {
            // Check whether a line was added or removed
            const didLineChange = (line: HTMLElement) =>
                line.classList.contains(DIFF_NAV_ADDED_CLASS) ||
                line.classList.contains(DIFF_NAV_REMOVED_CLASS);

            // Start the opposite of the first line so it's always included
            // (and looping around the bottom always jumps to the top first)
            let currentBlockHasChanges = !didLineChange(codeLines[0]);
            for (let line of codeLines) {
                // We're entering a new block when this line doesn't match the
                // previous block's state
                if (didLineChange(line) !== currentBlockHasChanges) {
                    currentBlockHasChanges = !currentBlockHasChanges;
                    if (currentBlockHasChanges || diffNavBlocks.length === 0) {
                        diffNavBlocks.push(line);
                    }
                }
            }
        }

        // Wire up a keyboard listener to navigate between blocks
        if (diffNavBlocks.length > 0) {
            // Mutable state tracking whether navigation is enabled and the
            // currently navigated block index
            let diffNavEnabled = false;
            let diffNavIndex = 0;

            // mod in JS doesn't wrap negatives around so everyone does this
            const modulo = (n, m) => (n % m + m) % m;

            // Build functions to move to the next block `delta` away from the
            // current index for forward/backward navigation
            const makeMover =
                delta =>
                    () => {
                        diffNavBlocks[diffNavIndex].classList.remove(DIFF_NAV_FOCUSED_CLASS);
                        diffNavIndex = modulo(diffNavIndex + delta, diffNavBlocks.length)
                        diffNavBlocks[diffNavIndex].scrollIntoView({ behavior: "smooth", block: "center" });
                        diffNavBlocks[diffNavIndex].classList.add(DIFF_NAV_FOCUSED_CLASS);
                    };
            const diffNavMoveNext = makeMover(1);
            const diffNavMovePrev = makeMover(-1);

            // Always listen for key presses so we're listening to enable
            window.addEventListener(
                'keydown',
                e => {
                    // Ignore key presses when focused on any element (i.e.,
                    // when typing in a TEXTAREA)
                    if (window.document.activeElement === window.document.body) {
                        if (e.getModifierState(DIFF_NAV_ENABLE_KEY_MOD) && e.key === DIFF_NAV_ENABLE_KEY) {
                            // Toggle whether navigation is enabled
                            diffNavEnabled = !diffNavEnabled;
                        } else if (diffNavEnabled && e.key === DIFF_NAV_NEXT_KEY) {
                            diffNavMoveNext();
                        } else if (diffNavEnabled && e.key === DIFF_NAV_PREV_KEY) {
                            diffNavMovePrev();
                        }
                    }
                },
                // Don't capture events
                false);
        }
    }
});
