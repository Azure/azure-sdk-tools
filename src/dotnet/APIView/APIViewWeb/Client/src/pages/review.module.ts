//---------------------------------------------------------------------------------
// Funtion for managing expanging / collapseing of CodeLine Sections
//---------------------------------------------------------------------------------

export function getSectionHeadingRow(event: JQuery.ClickEvent<HTMLElement>)
{
    return $(event.currentTarget).parents(".code-line").first();
}




//---------------------------------------------------------------------------------
// Managing CodeLine Section / SubSection states (expand vs collapse) using cookies
//---------------------------------------------------------------------------------

export enum CodeLineSectionState { shown, hidden }

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