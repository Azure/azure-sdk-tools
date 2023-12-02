import Split from "split.js";
import * as hp from "../shared/helpers";

/* Hide some of the option switched (checkbox) when not needed */
export function hideCheckboxesIfNotApplicable() {
    if ($(".documentation").length == 0) {
        $("#show-documentation-component").hide();
    }
    if ($(".hidden-api-toggleable").length == 0) {
        $("#show-hidden-api-component").hide();
    }
}

/* Split left and right review panes using split.js */
export function splitReviewPageContent() {
    const rl = $('#review-left');
    const rr = $('#review-right');

    if (rl.length && rr.length) {
        Split(['#review-left', '#review-right'], {
        direction: 'horizontal',
        sizes: [17, 83],
        elementStyle: (dimension, size, gutterSize) => {
            return {
                'flex-basis': `calc(${size}% - ${gutterSize}px`
            }
        },
        gutterStyle: (dimension, gutterSize) => {
            return {
                'flex-basis': `${gutterSize}px`
            }
        }
        });
    }
}

//-------------------------------------------------------------------------------------------------
// Funtions for managing expanging / collapseing of CodeLine Sections / subSections
//-------------------------------------------------------------------------------------------------

export enum CodeLineSectionState { shown, hidden }

/**
* Get the section row that was clicked
* @param { JQuery.ClickEvent<HTMLElement> } event that triggered the change of state
* @return { JQuery<HTMLElement> } the row that is being updated
*/
export function getSectionHeadingRow(event: JQuery.ClickEvent<HTMLElement>)
{
    return $(event.currentTarget).parents(".code-line").first();
}

/**
* Update Icons that indicate if Section is Expanded or Collapsed
* @param { CodeLineSectionState } setTo - the section state after update
* @param { JQuery<HTMLElement> } caretIcon - the icon (button) that controls the section state
* @param { JQuery<HTMLElement> } headingRow - the heading of the section
*/
function updateSectionHeadingIcons(setTo: CodeLineSectionState, caretIcon : JQuery<HTMLElement>,
    headingRow : JQuery<HTMLElement>) {
    if (setTo == CodeLineSectionState.shown) {
        caretIcon.removeClass("fa-angle-right");
        caretIcon.addClass("fa-angle-down");
        headingRow.find(".row-fold-elipsis").addClass("d-none");
    }

    if (setTo == CodeLineSectionState.hidden) {
        caretIcon.removeClass("fa-angle-down");
        caretIcon.addClass("fa-angle-right");
        headingRow.find(".row-fold-elipsis").removeClass("d-none");
    }
}

/**
* Expand or Collapse CodeLine Top Level Sections
* @param { JQuery<HTMLElement> } headingRow - the heading row that controls the state of the section
* @param { any } sectionContent - row or rows whose state (hidden/shown) is managed by the headingRow
* @param { string } caretDirection - indicates the state of the section can end with "right" or "down"
* @param { JQuery<HTMLElement> } caretIcon - indicates the state of the section > (hidden) v (shown)
*/
function toggleSectionContent(headingRow : JQuery<HTMLElement>, sectionContent, caretDirection : string,
    caretIcon : JQuery<HTMLElement>) {
    const rowLineNumber = headingRow.find(".line-number>span").text();
    if (caretDirection.endsWith("right")) {
        // In case the section passed has already been replaced with more rows
        if (sectionContent.length == 1) {
            const sectionContentClass = sectionContent[0].className.replace(/\s/g, '.');
            const sectionCommentClass = sectionContentClass.replace("code-line.", "comment-row.");
            sectionContent = $(`.${sectionContentClass}`);
            sectionContent.push(...$(`.${sectionCommentClass}`));
        }

        $.each(sectionContent, function (index, value) {
            let rowClasses = $(value).attr("class");
            if (rowClasses) {
                if (rowClasses.match(/comment-row/)) {
                    // Ensure comment icon is shown on parent row that have comments in its subsection
                    let rowClassList = rowClasses.split(/\s+/);
                    let levelClass = rowClassList.find((c) => c.match(/lvl_[0-9]+_child_[0-9]+/));
                    if (levelClass)
                    {
                        let level = Number(levelClass.split('_')[1]) - 1;
                        let parent = $(value);
                        while (level > 0)
                        {
                            parent = parent.prevAll(`[class*='lvl_${level}_parent']:first`);
                            if (parent)
                            {
                                parent.find(".icon-comments").addClass("comment-in-section");
                            }
                            level--;
                        }

                    }
                }

                if (rowClasses.match(/lvl_1_/)) {
                    if (rowClasses.match(/comment-row/) && !$("#show-comments-checkbox").prop("checked")) {
                        hp.toggleCommentIcon($(value).attr("data-line-id")!, true);
                        return; // Dont show comment row if show comments setting is unchecked
                    }
                    $(value).removeClass("d-none");
                    $(value).find("svg").attr("height", `${$(value).height()}`);
                }
            }
        });

        // Add jump-lint event for classes
        addClickEventToClassesInSections();

        // Update section heading icons to open state
        updateSectionHeadingIcons(CodeLineSectionState.shown, caretIcon, headingRow);

        // maintain lineNumbers of shown headings in sessionStorage
        let shownSectionHeadingLineNumbers = sessionStorage.getItem("shownSectionHeadingLineNumbers") ?? "";
        shownSectionHeadingLineNumbers = updateCodeLineSectionState(shownSectionHeadingLineNumbers, rowLineNumber, CodeLineSectionState.shown);
        sessionStorage.setItem("shownSectionHeadingLineNumbers", shownSectionHeadingLineNumbers);
    }
    else {
        $.each(sectionContent, function (index, value) {
            let rowClasses = $(value).attr("class");
            if (rowClasses) {
                if (rowClasses.match(/lvl_[0-9]+_parent_/)) {
                    // Update all heading/parent rows to closed state before hiding it
                    let caretIcon = $(value).find(".row-fold-caret").children("i");
                    let lineNo = $(value).find(".line-number>span").text();
                    updateSectionHeadingIcons(CodeLineSectionState.hidden, caretIcon, $(value));

                    // maintain lineNumbers of shown headings in sessionStorage
                    let shownSubSectionHeadingLineNumbers = sessionStorage.getItem("shownSubSectionHeadingLineNumbers") ?? "";
                    shownSubSectionHeadingLineNumbers = updateCodeLineSectionState(shownSubSectionHeadingLineNumbers, lineNo, CodeLineSectionState.hidden);
                    sessionStorage.setItem("shownSubSectionHeadingLineNumbers", shownSubSectionHeadingLineNumbers)
                }
            }
            $(value).addClass("d-none");
        });

        // Update section heading icons to closed state
        updateSectionHeadingIcons(CodeLineSectionState.hidden, caretIcon, headingRow);

       // maintain lineNumbers of shown headings in sessionStorage
       let shownSectionHeadingLineNumbers = sessionStorage.getItem("shownSectionHeadingLineNumbers") ?? "";
       shownSectionHeadingLineNumbers = updateCodeLineSectionState(shownSectionHeadingLineNumbers, rowLineNumber, CodeLineSectionState.hidden);
       sessionStorage.setItem("shownSectionHeadingLineNumbers", shownSectionHeadingLineNumbers);
    }
}

/**
* Expand or Collapse CodeLine SubSections
* @param { JQuery<HTMLElement> } headingRow - the heading row that controls the state of the section
* @param { string } subSectionLevel - the level or depth of the subSection on the section tree
* @param { string } subSectionHeadingPosition - The position of the subSectionHeading i.e position among its siblings
* @param { string } subSectionContentClass - class of the subsection
* @param { string } caretDirection - indicates the state of the section can end with "right" or "down"
* @param { JQuery<HTMLElement> } caretIcon - indicates the state of the section > (hidden) v (shown)
* @param { string } linenumber - for the heading row
*/
function toggleSubSectionContent(headingRow : JQuery<HTMLElement>, subSectionLevel : string, subSectionHeadingPosition : string,
    subSectionContentClass : string, caretDirection : string, caretIcon : JQuery<HTMLElement>, lineNumber) {
    var subSectionDescendants = $(`.${subSectionContentClass}`);

    if (caretDirection.endsWith("right")) {
        var startShowing = false;
        $.each(subSectionDescendants, function (index, value) {
            var rowClasses = $(value).attr("class");
            var rowLineNumber = $(value).find(".line-number>span").text();
            if (rowClasses) {
                if (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${subSectionHeadingPosition}`)) && rowLineNumber == lineNumber) {
                    startShowing = true;
                }
                    
                if (startShowing && (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${Number(subSectionHeadingPosition) + 1}`))
                    || rowClasses.match(new RegExp(`lvl_${subSectionLevel}_child_${Number(subSectionHeadingPosition) + 1}`))
                    || rowClasses.match(new RegExp(`lvl_${Number(subSectionLevel) - 1}_`)))) {
                    return false;
                }
                    

                // Show only immediate descendants
                if (startShowing) {
                    if (rowClasses.match(new RegExp(`lvl_${Number(subSectionLevel) + 1}_`))) {
                        if (rowClasses.match(/comment-row/) && !$("#show-comments-checkbox").prop("checked")) {
                            hp.toggleCommentIcon($(value).attr("data-line-id")!, true);
                            return; // Dont show comment row if show comments setting is unchecked
                        }

                        $(value).removeClass("d-none");
                        let rowHeight = $(value).height() ?? 0;
                        $(value).find("svg").attr("height", `${rowHeight}`);
                    }
                }
            }
        });

        // Add jump-lint event for classes
        addClickEventToClassesInSections();

        // Update section heading icons to open state
        updateSectionHeadingIcons(CodeLineSectionState.shown, caretIcon, headingRow);

        // maintain lineNumbers of shown headings in session storage
        let shownSubSectionHeadingLineNumbers = sessionStorage.getItem("shownSubSectionHeadingLineNumbers") ?? "";
        shownSubSectionHeadingLineNumbers = updateCodeLineSectionState(shownSubSectionHeadingLineNumbers, lineNumber, CodeLineSectionState.shown);
        sessionStorage.setItem("shownSubSectionHeadingLineNumbers", shownSubSectionHeadingLineNumbers);
    }
    else {
        var startHiding = false;
        $.each(subSectionDescendants, function (index, value) {
            var rowClasses = $(value).attr("class");
            var rowLineNumber = $(value).find(".line-number>span").text();
            if (rowClasses) {
                if (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${subSectionHeadingPosition}`)) && rowLineNumber == lineNumber) {
                    startHiding = true;
                }
                    
                if (startHiding && (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${Number(subSectionHeadingPosition) + 1}`))
                    || rowClasses.match(new RegExp(`lvl_${subSectionLevel}_child_${Number(subSectionHeadingPosition) + 1}`))
                    || rowClasses.match(new RegExp(`lvl_${Number(subSectionLevel) - 1}_`)))) {
                        return false;
                }
                

                if (startHiding) {
                    let descendantClasses = rowClasses.split(' ').filter(c => c.match(/lvl_[0-9]+_child_.*/))[0];
                    if (descendantClasses) {
                        let descendantLevel = descendantClasses.split('_')[1];
                        if (/^\d+$/.test(descendantLevel)) {
                            if (Number(descendantLevel) > Number(subSectionLevel)) {
                                $(value).addClass("d-none");
                                if (rowClasses.match(/lvl_[0-9]+_parent_.*/)) {
                                    // Update all heading/parent rows to closed state before hiding it
                                    let caretIcon = $(value).find(".row-fold-caret").children("i");
                                    let lineNo = $(value).find(".line-number>span").text();
                                    updateSectionHeadingIcons(CodeLineSectionState.hidden, caretIcon, $(value));

                                    // maintain lineNumbers of shown headings in sessionStorage
                                    let shownSubSectionHeadingLineNumbers = sessionStorage.getItem("shownSubSectionHeadingLineNumbers") ?? "";
                                    shownSubSectionHeadingLineNumbers = updateCodeLineSectionState(shownSubSectionHeadingLineNumbers, lineNo, CodeLineSectionState.hidden);
                                    sessionStorage.setItem("shownSubSectionHeadingLineNumbers", shownSubSectionHeadingLineNumbers);
                                }
                            }
                        }
                    }
                }
            }
        });

        // Update section heading icons to closed state
        updateSectionHeadingIcons(CodeLineSectionState.hidden, caretIcon, headingRow);

        // maintain lineNumbers of shown headings in sessionStorage
        let shownSubSectionHeadingLineNumbers = sessionStorage.getItem("shownSubSectionHeadingLineNumbers") ?? "";
        shownSubSectionHeadingLineNumbers = updateCodeLineSectionState(shownSubSectionHeadingLineNumbers, lineNumber, CodeLineSectionState.hidden);
        sessionStorage.setItem("shownSubSectionHeadingLineNumbers", shownSubSectionHeadingLineNumbers);
    }
}

/**
* Updates the state of the section and subSections logically under the headingRow
* @param { JQuery<HTMLElement> } headingRow - the heading row that controls the state of the section
*/
export function toggleCodeLines(headingRow : JQuery<HTMLElement>) {
    if (headingRow.attr('class')) {
        const headingRowClasses = hp.getElementClassList(headingRow);
        const caretIcon = headingRow.find(".row-fold-caret").children("i");
        const caretDirection = hp.getElementClassList(caretIcon).filter(c => c.startsWith('fa-angle-'))[0];
        const subSectionHeadingClass = headingRowClasses.filter(c => c.startsWith('code-line-section-heading-'))[0];
        const subSectionContentClass = headingRowClasses.filter(c => c.startsWith('code-line-section-content-'))[0];

        if (subSectionHeadingClass) {
            const sectionKey = subSectionHeadingClass.replace("code-line-section-heading-", "")
            const sectionKeyA = headingRowClasses.filter(c => c.startsWith('rev-a-heading-'))[0]?.replace('rev-a-heading-', '');
            const sectionKeyB = headingRowClasses.filter(c => c.startsWith('rev-b-heading-'))[0]?.replace('rev-b-heading-', '');

            if (/^\d+$/.test(sectionKey)) {
                var sectionContent = $(`.code-line-section-content-${sectionKey}`);
                if (sectionContent.hasClass("section-loaded")) {
                    toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
                }
                else {
                    let uri = '?handler=codelinesection';
                    const uriPath = location.pathname.split('/');
                    const reviewId = uriPath[uriPath.length - 1];
                    const revisionId = new URLSearchParams(location.search).get("revisionId");
                    const diffRevisionId = new URLSearchParams(location.search).get("diffRevisionId");
                    const diffOnly = new URLSearchParams(location.search).get("diffOnly");
                    uri = uri + '&id=' + reviewId + '&sectionKey=' + sectionKey;
                    if (revisionId)
                        uri = uri + '&revisionId=' + revisionId;
                    if (diffRevisionId)
                        uri = uri + '&diffRevisionId=' + diffRevisionId;
                    if (diffOnly)
                        uri = uri + '&diffOnly=' + diffOnly;
                    if (sectionKeyA)
                        uri = uri + '&sectionKeyA=' + sectionKeyA;
                    if (sectionKeyB)
                        uri = uri + '&sectionKeyB=' + sectionKeyB;

                    const loadingMarkUp = "<td class='spinner-border spinner-border-sm ms-4' role='status'><span class='sr-only'>Loading...</span></td>";
                    const failedToLoadMarkUp = "<div class='alert alert-warning alert-dismissible fade show' role='alert'>Failed to load section. Refresh page and try again.</div>";
                    if (sectionContent.children(".spinner-border").length == 0) {
                        sectionContent.children("td").after(loadingMarkUp);
                    }
                    sectionContent.removeClass("d-none");

                    const request = $.ajax({ url: uri });
                    request.done(function (partialViewResult) {
                        sectionContent.replaceWith(partialViewResult);
                        toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
                        addCodeLineToggleEventHandlers();
                    });
                    request.fail(function () {
                        if (sectionContent.children(".alert").length == 0) {
                            sectionContent.children(".spinner-border").replaceWith(failedToLoadMarkUp);
                        }
                    });
                    return request;
                }
            }
        }

        if (subSectionContentClass) {
            const subSectionClass = headingRowClasses.filter(c => c.match(/.*lvl_[0-9]+_parent.*/))[0];
            const lineNumber = headingRow.find(".line-number>span").text();
            if (subSectionClass) {
                const subSectionLevel = subSectionClass.split('_')[1];
                const subSectionHeadingPosition = subSectionClass.split('_')[3];
                if (/^\d+$/.test(subSectionLevel) && /^\d+$/.test(subSectionHeadingPosition)) {
                    toggleSubSectionContent(headingRow, subSectionLevel, subSectionHeadingPosition, subSectionContentClass, caretDirection, caretIcon, lineNumber);
                }
            }
        }
    }
}

/* Add event handler for Expand / Collapse of CodeLine Sections and SubSections */
export function addCodeLineToggleEventHandlers() {
    $('.row-fold-elipsis, .row-fold-caret').on('click', function (event) {
        event.preventDefault();
        event.stopImmediatePropagation();
        var headingRow = getSectionHeadingRow(event);
        toggleCodeLines(headingRow);
    });
}

/**
* Updates Browser Cookie with the State of the Codeline Section (hidden or shown)
* @param { String } cookieValue
* @param { String } lineNumber
* @param { CodeLineSectionState } state
* @return { string } updatedCookieValue
*/
export function updateCodeLineSectionState(cookieValue: string, lineNumber: string, state: CodeLineSectionState) {
    const expandedSections = cookieValue.split(',');
    const updatedCookieValue : string[] = [];
    let updateComplete : boolean = false;
    expandedSections.forEach((val) => {
        if (val) {
            if (val !== lineNumber && !isNaN(Number(val))) {
                updatedCookieValue.push(val);
            } 
            else {
                if (state == CodeLineSectionState.shown && !isNaN(Number(val))) {
                    updatedCookieValue.push(val);
                }
                updateComplete = true;
            }
        }
    });
    if (!updateComplete && state == CodeLineSectionState.shown)
        updatedCookieValue.push(lineNumber);

    return updatedCookieValue.join(',');
}

/**
* Read section and subSection state (lineNumbers) from cookies and reload them
*/
export function loadPreviouslyShownSections() {
    const shownSectionHeadingLineNumbers = sessionStorage.getItem("shownSectionHeadingLineNumbers") ?? "";
    
    // Load each section whose heading line number is present in the cookie
    const elementsWithLineNumbers = Array.from($(".line-number"));
    for (const lineNumber of shownSectionHeadingLineNumbers.split(',')) {
        const lineNoElement = elementsWithLineNumbers.filter(element => $(element).find('span').text() === lineNumber);
        const lineDetailsBtnCell = $(lineNoElement).siblings("td .line-details-button-cell");
        for (const element of Array.from($(lineDetailsBtnCell))) {
            const rowCaretCell = Array.from($(element).children(".row-fold-caret"));
            if (rowCaretCell.length > 0)
            {
                rowCaretCell[0].click();
            }
        }
    }

    const shownSubSectionHeadingLineNumbers = sessionStorage.getItem("shownSubSectionHeadingLineNumbers") ?? "";

    // Load subSections as the headings become visible on the page
    const subSectionHeadingLineNumberQueue = shownSubSectionHeadingLineNumbers.split(',');
    const intervalID = setInterval((subSectionHeadingLineNumberQueue) => {
        if (subSectionHeadingLineNumberQueue.length > 0)
        {
            const lineNumber = subSectionHeadingLineNumberQueue.shift();
            const lineNoElement = Array.from($(".line-number")).filter(element => $(element).find('span').text() === lineNumber);
            if (lineNoElement.length > 0) {
                const lineDetailsBtnCell = $(lineNoElement).siblings("td .line-details-button-cell");
                for (const element of Array.from($(lineDetailsBtnCell))) {
                    const rowCaretCell = Array.from($(element).children(".row-fold-caret"));
                    if (rowCaretCell.length > 0)
                    {
                        rowCaretCell[0].click();
                    }
                }
            }
            else {
                subSectionHeadingLineNumberQueue.push(lineNumber!);
            }
        }
        else {
            clearInterval(intervalID);
        }
    }, 1000, subSectionHeadingLineNumberQueue);

    // remove toast
    $("#loadPreviouslyShownSectionsToast").remove();
}

/**
* Call a callback function after expanging a codeline section
* @param { String } targetAnchorId
* @param { Function } callback
*/
export function runAfterExpandingCodeline(targetAnchorId, callback) {
  var targetAnchor = document.getElementById(targetAnchorId);
  if (targetAnchor) {
    var targetAnchorRow = $(targetAnchor).parents(".code-line").first();
    var rowFoldSpan = targetAnchorRow.find(".row-fold-caret");
    if (rowFoldSpan.length > 0) {
      var caretIcon = rowFoldSpan.children("i");
      var caretClasses = caretIcon.attr("class");
      var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
      if (caretDirection.endsWith("right")) {
        window.location.hash = `#${targetAnchorId}`;
        $.when(toggleCodeLines(targetAnchorRow)).then(callback);
      }
    }
  }
}

/**
* Adds custom click event to classes in codeline sections
*/
export function addClickEventToClassesInSections() {
  $(".code-inner li a").off("click").on("click", function (e) {
        e.preventDefault();
        const anchorHash = $(this).attr("href");
        if (anchorHash) {
            const targetAnchorId = anchorHash.replace('#', '');
            const definitionsAnchorId = targetAnchorId.substring(0, targetAnchorId.lastIndexOf("Definitions") + "Definitions".length)
            const target = $(`[data-line-id="${targetAnchorId}"]`);
            if (target.length == 0 || target.hasClass("d-none")) {
                runAfterExpandingCodeline(definitionsAnchorId, function () {
                window.location.hash = anchorHash;
                });
            }
            else {
                window.location.hash = anchorHash;
            }
        }
  });
}

/**
 * Add Select Event Handlers to API Revision Select
 */
export function addSelectEventToAPIRevisionSelect() {
  $('#revision-select, #diff-select').each(function (index, value) {
    $(this).on('change', function () {
      var url = $(this).find(":selected").val();
      if (url) {
        window.location.href = url as string;
      }
    });
  });
}

/**
* Check if targetAnchor is present, if its not present, expand the section and scroll to the targetAnchor
* @param { String } uriHash  the hash/id of the anchor we are looking for
* @param { String [] } mainSections the sections to be expanded if the targetAnchor is not present or hidden
*/
export function findTargetAnchorWithinSections(uriHash : string, mainSections: string[] = ["Paths", "Definitions"]) {
    if (uriHash.startsWith('#'))
        uriHash = uriHash.replace('#', '');

    uriHash = decodeURIComponent(uriHash);
    let targetAnchor = $(`[id="${uriHash}"]`);

    if (targetAnchor.length == 0)
        targetAnchor = $(`[data-line-id="${uriHash}"]`);

    if (targetAnchor.length == 0) {
        if (mainSections.length == 0) {
            return;
        }

        const anchorBase = uriHash.substring(0, uriHash.indexOf("json") + "json".length);
        let anchorToExpand = anchorBase;
        if (uriHash.includes("-Paths-")) {
            anchorToExpand = anchorToExpand + "-" + mainSections.shift();
        }
        else {
            anchorToExpand = anchorToExpand + "-" + mainSections.pop();
        }
        runAfterExpandingCodeline(anchorToExpand, function() {
            findTargetAnchorWithinSections(uriHash, mainSections)
        });
    }
    else {
        const anchorCodeLineRow = $(targetAnchor[0]).hasClass("code-line") ? $(targetAnchor[0]) : $(targetAnchor[0]).parents(".code-line").first();

        if (anchorCodeLineRow.hasClass("d-none")) {
            // Get anchor parents up to a level thats not hidden, expand each before scrolling to the anchor
            const parentsToExpand = new Map<number, JQuery<HTMLElement>>();
            let parent = anchorCodeLineRow.prevAll(`[class*='parent']:first`);
            let parentClass = parent.attr("class")?.split(/\s+/).find((c) => c.match(/lvl_[0-9]+_parent_[0-9]+/));
            let parentLevel = Number(parentClass?.split("_")[1]);

            while(parentLevel > 0)
            {
                if (!parentsToExpand.has(parentLevel))
                    parentsToExpand.set(parentLevel, parent);

                if (!parent.hasClass("d-none"))
                    break;

                parent = parent.prevAll(`[class*='parent']:first`);
                parentClass = parent.attr("class")?.split(/\s+/).find((c) => c.match(/lvl_[0-9]+_parent_[0-9]+/));
                parentLevel = Number(parentClass?.split("_")[1]);
            }

            while (parentsToExpand.size > 0)
            {
                const key = Math.min(...parentsToExpand.keys());
                const rowToExpand = parentsToExpand.get(key);

                // Expand Row
                var rowFoldSpan = rowToExpand!.find(".row-fold-caret");
                if (rowFoldSpan.length > 0) {
                var caretIcon = rowFoldSpan.children("i");
                var caretClasses = caretIcon.attr("class");
                var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
                    if (caretDirection.endsWith("right")) {
                        toggleCodeLines(rowToExpand!)
                    }
                }

                parentsToExpand.delete(key);
            }
            window.location.hash = `#${uriHash}`;
        }
        else {
            window.location.hash = `#${uriHash}`;
        }

    }
}

/**
 * adds the @approver to @lowerTextSpan of review page
 * @param lowerTextSpan HTMLElement of the span that contains who approved the review or pending approval
 * @param approvedByText string that comes before list of approvers
 * @param approverHref relative href of user's apiview profile
 * @param approver GitHub username of the review approver 
 */
export function addApprover(lowerTextSpan: HTMLElement, approvedByText: string, approverHref: string, approver: any) {
    if (lowerTextSpan.textContent?.includes(approvedByText)) {
        lowerTextSpan.append(" , ");
    } else {
        lowerTextSpan.textContent = approvedByText;
        addApprovedBorder();
    }
    addApproverHrefToApprovers(lowerTextSpan, approverHref, approver);
  }
  
  /**
   * Removes the @approver from @lowerTextSpan of review page
   * @param lowerTextSpan HTMLElement of the span that contains who approved the review or pending approval
   * @param approver GitHub username of the review approver
   * @param approvalPendingText string of approval pending text to use when removing the last approver
   */
  export function removeApprover(lowerTextSpan: HTMLElement, approver: string, approvalPendingText: string) {
    let children = lowerTextSpan.children;
    let numApprovers = children.length;
  
    if (numApprovers > 1) {
      removeApproverFromApproversList(children, approver);
    } else {
      lowerTextSpan.textContent = approvalPendingText;
      removeApprovalBorder();
    }
  }
  
  /**
   * adds the @approver with a hyperlink to their apiview profile to @lowerTextSpan
   */
  export function addApproverHrefToApprovers(lowerTextSpan: HTMLElement, approverHref: string, approver: any) {
    $(lowerTextSpan).append('<a href="' + approverHref + '">' + approver + '</a>');
  }
  
  /**
   * adds the text above the approve button to indicate whether the current user approved the review
   */
  export function addUpperTextSpan(approvesCurrentRevisionText: string) {
    let $upperTextSpan = $("<span>").text(approvesCurrentRevisionText).addClass("small text-muted");
    let $upperTextForm = $("ul#approveCollapse form.form-inline");
    $upperTextForm.prepend($upperTextSpan);
  }
  
  /**
   * change the button state from a green "not approved" to grey "approved"
   */
  export function addButtonApproval() {
    let $approveBtn = $("form.form-inline button.btn.btn-success");
    $approveBtn.removeClass("btn-success");
    $approveBtn.addClass("btn-outline-secondary");
    $approveBtn.text("Revert API Approval");
  }
  
  /**
   * change the button state from a grey "approved" to green "not approved" 
   */
  export function removeButtonApproval() {
    let $approveBtn = $("form.form-inline button.btn.btn-outline-secondary");
    $approveBtn.removeClass("btn-outline-secondary");
    $approveBtn.addClass("btn-success");
    $approveBtn.text("Approve");
  }
  
  /**
   * change the review panel border state from grey "not approved" to green "approved"
   */
  export function addApprovedBorder() {
    let reviewLeft = $("#review-left");
    reviewLeft.addClass("review-approved");
    reviewLeft.removeClass("border");
    reviewLeft.removeClass("rounded-1");
  
    let reviewRight = $("#review-right");
    reviewRight.addClass("review-approved");
    reviewRight.removeClass("border");
    reviewRight.removeClass("rounded-1");
  }
  
  /**
   * change the review panel border state from green "approved" to grey "not approved"
   */
  export function removeApprovalBorder() {
    let $reviewLeft = $("#review-left");
    $reviewLeft.removeClass("review-approved");
    $reviewLeft.addClass("border");
    $reviewLeft.addClass("rounded-1");
  
    let $reviewRight = $("#review-right");
    $reviewRight.removeClass("review-approved");
    $reviewRight.addClass("border");
    $reviewRight.addClass("rounded-1");
  }
  
  /**
   * parse the approval spans for its existence and order
   * @param $approvalSpans may contain <upper text>, <approve button>, and/or <lower text>
   * @param approvedByText string for <lower text> that indicates preexisting approvers
   * @param approvalPendingText string for <lower text> that indicates no current approvers
   * @param approvesCurrentRevisionText string for <upper text> that indicates the current user did not approve
   * @returns a dictionary with the index of the upper and lower text elements. Value is -1 if an element does not exist.
   */
  export function parseApprovalSpanIndex($approvalSpans: JQuery<HTMLElement>, approvedByText: string, approvalPendingText: string, approvesCurrentRevisionText: string) {
    let indexResult = {
        "approvers": -1,
        "upperText": -1,
    };
  
    for (var i = 0; i < $approvalSpans.length; i++) {
      let content = $approvalSpans[i].textContent;
  
      if (!content) {
        return indexResult;
      }
  
      if (content.includes(approvedByText) || content.includes(approvalPendingText)) {
            indexResult["approvers"] = i;
        }
      if (content.includes(approvesCurrentRevisionText)) {
        indexResult["upperText"] = i;
        }
    }
  
    return indexResult;
  }
  
  /**
   * call when the current user approves the current review. removes the upper text 
   * @param upperTextIndex index of the upper text in @$approvalSpans
   * @param $approvalSpans span that includes revision approval block 
   */
  export function removeUpperTextSpan(upperTextIndex: number, $approvalSpans: JQuery<HTMLElement>) {
    if (upperTextIndex !== -1) {
        let upperTextSpan: HTMLElement = $approvalSpans[upperTextIndex];
      upperTextSpan.remove();
    }
  }
  
  /**
   * remove the @approver from list of @approvers 
   * @param approvers list of preexisting approvers
   * @param approver GitHub username of user to remove from the list 
   */
  export function removeApproverFromApproversList(approvers, approver) {
    for (var i = 0; i < approvers.length; i++) {
      if (approvers[i].innerHTML === approver) {
        if (i === 0) {
          approvers[i].nextSibling?.remove();
        } else {
          approvers[i].previousSibling?.remove();
        }
        approvers[i].remove();
        break;
      }
    }
  }
