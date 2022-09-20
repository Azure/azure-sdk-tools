$(() => {
    const INVISIBLE = "invisible";
    const SEL_CODE_DIAG = ".code-diagnostics";
    const SEL_COMMENT_ICON = ".icon-comments";
    const SEL_COMMENT_CELL = ".comment-cell";
    const COMMENT_CONTENT_BOX = ".new-comment-content";
    const COMMENT_TEXTBOX = ".new-thread-comment-text";

    let MessageIconAddedToDom = false;

    let CurrentUserSuggestionElements: HTMLElement[] = [];
    let CurrentUserSuggestionIndex = -1;

    // simple github username match
    const githubLoginTagMatch = /(\s|^)@([a-zA-Z\d-]+)/g;

    $(document).on("click", ".commentable", e => {
        showCommentBox(e.target.id);
        e.preventDefault();
    });

    $(document).on("click", ".line-comment-button", e => {
        let id = getElementId(e.target);
        if (id) {
            showCommentBox(id);
        }
        e.preventDefault();
    });

    $(document).on("click", ".comment-cancel-button", e => {
        let id = getElementId(e.target);
        if (id) {
            hideCommentBox(id);
            // if a comment was added and then cancelled, and there are no other
            // comments for the thread, we should remove the comments icon.
            if (getCommentsRow(id).find(SEL_COMMENT_CELL).length === 0) {
                getCodeRow(id).find(SEL_COMMENT_ICON).addClass(INVISIBLE);
            }
        }
        e.preventDefault();
    });

    $(document).on("click", "#show-comments-checkbox", e => {
        ensureMessageIconInDOM();
        toggleAllCommentsVisibility(e.target.checked);
    });

    $(document).on("click", "#show-system-comments-checkbox", e => {
        ensureMessageIconInDOM();
        toggleAllDiagnosticsVisibility(e.target.checked);
    });

    $(document).on("click", SEL_COMMENT_ICON, e => {
        let lineId = getElementId(e.target);
        if (lineId) {
            toggleSingleCommentAndDiagnostics(lineId);
        }
        e.preventDefault();
    });

    $(document).on("submit", "form[data-post-update='comments']", e => {
        const form = <HTMLFormElement><any>$(e.target);
        let lineId = getElementId(e.target);
        if (lineId) {
            let commentRow = getCommentsRow(lineId);
            let serializedForm = form.serializeArray();
            serializedForm.push({ name: "elementId", value: lineId });
            serializedForm.push({ name: "reviewId", value: getReviewId(e.target) });
            serializedForm.push({ name: "revisionId", value: getRevisionId(e.target) });
            serializedForm.push({ name: "taggedUsers", value: getTaggedUsers(e.target) });

            $.ajax({
                type: "POST",
                url: $(form).prop("action"),
                data: $.param(serializedForm)
            }).done(partialViewResult => {
                updateCommentThread(commentRow, partialViewResult);
                addCommentThreadNavigation();
            });
        }
        e.preventDefault();
    });

    $(document).on("click", ".review-thread-reply-button", e => {
        let lineId = getElementId(e.target);
        if (lineId) {
            showCommentBox(lineId);
        }
        e.preventDefault();
    });

    $(document).on("click", ".toggle-comments", e => {
        let lineId = getElementId(e.target);
        if (lineId) {
            toggleComments(lineId);
        }
        e.preventDefault();
    });

    $(document).on("click", ".js-edit-comment", e => {
        let commentId = getCommentId(e.target);
        if (commentId) {
            editComment(commentId);
        }
        e.preventDefault();
    });

    $(document).on("click", ".js-github", e => {
        let target = $(e.target);
        let repo = target.attr("data-repo");
        let language = getLanguage(e.target);

        // Fall back to the repo-language if we don't know the review language,
        // e.g.Go doesn't specify Language yet.
        if (!language) {
          language = target.attr("data-repo-language");
        }
        let commentElement = getCommentElement(getCommentId(e.target)!);
        let comment = commentElement.find(".js-comment-raw").html();
        let codeLine = commentElement.closest(".comment-row").prev(".code-line").find(".code");
        let apiViewUrl = "";

        // if creating issue from the convos tab, the link to the code element is in the DOM already.
        let commentUrlElem = codeLine.find(".comment-url");
        if (commentUrlElem.length) {
          apiViewUrl = location.protocol + '//' + location.host + escape(commentUrlElem.attr("href")!);
        }
        else {
          // otherwise we construct the link from the current URL and the element ID
          // Double escape the element - this is used as the URL back to API View and GitHub will render one layer of the encoding.
          apiViewUrl = window.location.href.split("#")[0] + "%23" + escape(escape(getElementId(commentElement[0])!));
        }

        let issueBody = escape("```" + language + "\n" + codeLine.text().trim() + "\n```\n#\n" + comment);
        // TODO uncomment below once the feature to support public ApiView Reviews is enabled.
        //+ "\n#\n")
        //+ "[Created from ApiView comment](" + apiViewUrl + ")";

        window.open(
            "https://github.com/Azure/" + repo + "/issues/new?" +
            "&body=" + issueBody,
            '_blank');
        e.preventDefault();
    });

    $(document).on("keydown", ".new-thread-comment-text", e => {
        // form submit on ctrl + enter
        if (e.ctrlKey && (e.keyCode === 10 || e.keyCode === 13)) {
            const form = $(e.target).closest("form");
            if (form) {
                form.submit();
            }
            e.preventDefault();
            return;
        }

        // current value of form not including key currently pressed
        let currentVal: string = e.target.value;
        // system keys should allow the list to be updated, but should not be added as a key
        // currentVal doesn't include the key to be added, though, so we need to add it manually
        if(!(e.key === "Enter" || e.key === "Backspace" || e.key === "Tab" || e.key === "Alt" || e.key === "Control" || e.key === "Shift" || e.key === "Tab" || e.key === "ArrowDown" || e.key === "ArrowUp")) {
            currentVal += e.key;
            CurrentUserSuggestionElements = [];
            CurrentUserSuggestionIndex = -1;
        } else if(e.key === "Backspace") {
            currentVal = currentVal.substring(0, currentVal.length - 1); // remove last char
        }

        // current caret position in textarea
        const caretPosition = (<HTMLInputElement>e.target).selectionStart;
        

        // returned value from regex exec
        // index 0 is the entire match, index 1 and 2 are capture groups
        let matchArray;

        // gh username without @
        let matchName;
        
        // list of suggested users container
        const suggestionBox = $(e.target).parent().find(".tag-user-suggestion");
        
        // mirror box helps calculate x and y to place suggestion box at
        const mirrorBox = $(e.target).parent().find(".new-thread-comment-text-mirror");

        if(!suggestionBox.hasClass("d-none")) {
            // if suggestion box is visible and user pressed tab or enter
            if(e.key === "Tab" || e.key === "Enter") {
                // trigger click event (adds to box)
                CurrentUserSuggestionElements[CurrentUserSuggestionIndex].click();
                e.preventDefault();
                e.target.focus();
                return;
            } else if(e.key === "ArrowDown") {
                if(CurrentUserSuggestionIndex + 1 < CurrentUserSuggestionElements.length) {
                    $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).removeClass("tag-user-suggestion-username-targetted");
                    CurrentUserSuggestionIndex++;
                    $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).addClass("tag-user-suggestion-username-targetted");
                }
                e.preventDefault();
                return;
            } else if(e.key === "ArrowUp") {
                if(CurrentUserSuggestionIndex > 0) {
                    $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).removeClass("tag-user-suggestion-username-targetted");
                    CurrentUserSuggestionIndex--;
                    $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).addClass("tag-user-suggestion-username-targetted");
                }
                e.preventDefault();
                return;
            }
        }

        if(caretPosition !== null) {
            let changed = false;

            // get y position to place suggestion box
            mirrorBox.css('width', (<HTMLTextAreaElement>e.target).offsetWidth + "px");

            const upToCurrent = currentVal.substring(0, caretPosition + 1);
            mirrorBox.get(0).innerText = upToCurrent;

            const boxHeight = <number>mirrorBox.outerHeight();
            let top = boxHeight;

            // account for vertical offset from padding and overflow handling
            if(top < (<HTMLTextAreaElement>e.target).offsetHeight) {
                top -= 12;
            } else {
                top = (<HTMLTextAreaElement>e.target).offsetHeight;
                top += 6;
            }

            mirrorBox.css({'width': 'auto'});
            mirrorBox.get(0).innerText = upToCurrent.split('\n')[upToCurrent.split('\n').length - 1];

            const left = (<number>mirrorBox.innerWidth()) % ((<HTMLTextAreaElement>e.target).clientWidth - 24);

            // reset regex last checked index
            githubLoginTagMatch.lastIndex = 0;
            while ((matchArray = githubLoginTagMatch.exec(currentVal)) !== null) {
                // matchArray[0] = whole matched item
                // matchArray[1] = empty or whitespace (matched to start of string or single whitespace before tag)
                // matchArray[2] = tag excluding @ symbol
                const end = githubLoginTagMatch.lastIndex; // last index = end of string
                const stringLength = matchArray[2].length + 1; // add length of @ char
                const start = end - stringLength;
                
                // if caret is inside tagging user area
                if(caretPosition >= start && caretPosition < end) {
                    changed = true;
                    matchName = matchArray[2];

                    suggestionBox.removeClass("d-none");

                    suggestionBox.css({top: `${top}px`, left: `${left}px`});
                    
                    $(".tag-user-suggestion-username").addClass("d-none");
                    $(".tag-user-suggestion-username").removeClass("tag-user-suggestion-username-targetted");
                    let shownCount = 0;
                    const children = suggestionBox.children();

                    for(let child of children) {
                        if($(child).text().toLowerCase().includes(matchName.toLowerCase())) {
                            // display child
                            $(child).removeClass("d-none");

                            // add child to current list
                            CurrentUserSuggestionElements.push(child);

                            if(CurrentUserSuggestionElements.length === 1) {
                                CurrentUserSuggestionIndex = 0;
                                $(child).addClass("tag-user-suggestion-username-targetted");
                            }

                            // remove any other click handlers button might have
                            $(child).off('click');
                            $(child).on('click', (ev) => {
                                let currentValue = (<HTMLInputElement>e.target).value;

                                const newValue = currentValue.substring(0, start) + $(ev.target).text() + currentValue.substring(end, currentValue.length);
                                (<HTMLInputElement>e.target).value = newValue;
                                suggestionBox.addClass("d-none");

                                e.target.focus();
                            });

                            // load user image if image hasn't been loaded already
                            const imgChild = $(child).find('img')[0];
                            if($(imgChild).attr('src') === undefined) {
                                $(imgChild).attr('src', $(imgChild).data('src'));
                            }
                            shownCount++;
                            // show a maximum of 5 suggestions
                            if(shownCount === 5) {
                                break;
                            }
                        }

                    }
                    if(shownCount === 0) {
                        suggestionBox.addClass("d-none");
                    }
                    break;
                }
            }
            
            if(changed === false && !suggestionBox.hasClass("d-none")) {
                suggestionBox.addClass("d-none");
                CurrentUserSuggestionElements = [];
                CurrentUserSuggestionIndex = -1;
            }
        }
    });

    addEventListener("hashchange", e => {
        highlightCurrentRow();
    });

    addEventListener("load", e => {
        highlightCurrentRow();
        addCommentThreadNavigation();
    });

    function highlightCurrentRow() {
        if (location.hash.length < 1) return;
        var row = getCodeRow(location.hash.substring(1));
        row.addClass("active");
        row.on("animationend", () => {
            row.removeClass("active");
        });
    }

    function getReviewId(element: HTMLElement) {
        return getParentData(element, "data-review-id");
    }

    function getLanguage(element: HTMLElement) {
      return getParentData(element, "data-language");
    }

    function getRevisionId(element: HTMLElement) {
        return getParentData(element, "data-revision-id");
    }

    function getTaggedUsers(element: HTMLFormElement): string[] {
        githubLoginTagMatch.lastIndex = 0;
        let matchArray;
        const taggedUsers: string[] = [];

        const currentValue = new FormData(element).get("commentText") as string;
        while((matchArray = githubLoginTagMatch.exec(currentValue)) !== null) {
            taggedUsers.push(matchArray[2]);
        }
        return taggedUsers;
    }

    function getElementId(element: HTMLElement) {
        return getParentData(element, "data-line-id");
    }

    function getCommentId(element: HTMLElement) {
        return getParentData(element, "data-comment-id");
    }

    function getParentData(element: HTMLElement, name: string) {
        return $(element).closest(`[${name}]`).attr(name);
    }

    function toggleComments(id: string) {
        $(getCommentsRow(id)).find(".comment-holder").toggle();
    }

    function editComment(commentId: string) {
        let commentElement = $(getCommentElement(commentId));
        let commentText = commentElement.find(".js-comment-raw").html();
        let template = createCommentEditForm(commentId, commentText);
        commentElement.replaceWith(template);
    }

    function getCommentElement(commentId: string) {
        return $(`.review-comment[data-comment-id='${commentId}']`);
    }

    function getCommentsRow(id: string) {
        return $(`.comment-row[data-line-id='${id}']`);
    }

    function getCodeRow(id: string) {
        return $(`.code-line[data-line-id='${id}']`);
    }

    function getDiagnosticsRow(id: string) {
        return $(`.code-diagnostics[data-line-id='${id}']`);
    }

    function hideCommentBox(id: string) {
        let commentsRow = getCommentsRow(id);
        commentsRow.find(".review-thread-reply").show();
        commentsRow.find(".comment-form").hide();
    }

    function showCommentBox(id: string) {
        let commentForm;
        let commentsRow = getCommentsRow(id);

        if (commentsRow.length === 0) {
            commentForm = createCommentForm();
            commentsRow =
                $(`<tr class="comment-row" data-line-id="${id}">`)
                    .append($("<td colspan=\"3\">")
                        .append(commentForm));

            commentsRow.insertAfter(getDiagnosticsRow(id).get(0) || getCodeRow(id).get(0));
        }
        else {
            // there is a comment row - insert form
            let reply = $(commentsRow).find(".review-thread-reply");
            commentForm = $(commentsRow).find(".comment-form");
            if (commentForm.length === 0) {
                commentForm = $(createCommentForm()).insertAfter(reply);
            }
            reply.hide();
            commentForm.show();
            commentsRow.show(); // ensure that entire comment row isn't being hidden
        }

        $(getDiagnosticsRow(id)).show(); // ensure that any code diagnostic for this row is shown in case it was previously hidden

        // If comment checkbox is unchecked, show the icon for new comment
        if (!($("#show-comments-checkbox").prop("checked"))) {
            toggleCommentIcon(id, true);
        }

        commentForm.find(".new-thread-comment-text").focus();
    }

    function createCommentForm() {
        return $("#js-comment-form-template").children().clone();
    }

    function createCommentEditForm(commentId: string, text: string) {

        let form = $("#js-comment-edit-form-template").children().clone();
        form.find(".js-comment-id").val(commentId);
        form.find(".new-thread-comment-text").html(text);
        return form;
    }

    function updateCommentThread(commentBox, partialViewResult) {
        partialViewResult = $.parseHTML(partialViewResult);
        $(commentBox).replaceWith(partialViewResult);
        return false;
    }

    function toggleAllCommentsVisibility(showComments: boolean) {
        $(SEL_COMMENT_CELL).each(function () {
            var id = getElementId(this);
            if (id) {
                getCommentsRow(id).toggle(showComments);
                toggleCommentIcon(id, !showComments);
            }
        });
    }

    function toggleAllDiagnosticsVisibility(showComments: boolean) {
        $(SEL_CODE_DIAG).each(function () {
            var id = getElementId(this);
            if (id) {
                getDiagnosticsRow(id).toggle(showComments);
                toggleCommentIcon(id, !showComments);
            }
        });
    }

    function toggleSingleCommentAndDiagnostics(id: string) {
        getCommentsRow(id).toggle();
        getDiagnosticsRow(id).toggle();
    }

    function ensureMessageIconInDOM() {
        if (!MessageIconAddedToDom) {
            $(".line-comment-button-cell").append(`<span class="icon icon-comments ` + INVISIBLE + `"><i class="far fa-comment-alt pt-1 pl-1"></i></span>`);
            MessageIconAddedToDom = true;
        }
    }

    function toggleCommentIcon(id, show: boolean) {
        getCodeRow(id).find(SEL_COMMENT_ICON).toggleClass(INVISIBLE, !show);
    }

    function addCommentThreadNavigation(){
        var commentRows = $('.comment-row');
        commentRows.each(function (index) {
            var commentThreadAnchorId = "comment-thread-" + index;
            $(this).find('.comment-thread-anchor').first().prop('id', commentThreadAnchorId);

            var commentNavigationButtons = $(this).find('.comment-navigation-buttons').last();
            commentNavigationButtons.empty();

            var nextCommentThreadAnchor = "comment-thread-" + (index + 1);
            var previousCommentThreadAnchor = "comment-thread-" + (index - 1);

            if (commentRows.length != 1)
            {
                if (index == 0) {
                  commentNavigationButtons.append(`<a class="btn btn-outline-dark" href="#${nextCommentThreadAnchor}" title="Next Comment"><i class="fa fa-chevron-down" aria-hidden="true"></i></a>`)
                }
                else if (index == commentRows.length - 1) {
                  commentNavigationButtons.append(`<a class="btn btn-outline-dark" href="#${previousCommentThreadAnchor}" title="Previous Comment"><i class="fa fa-chevron-up" aria-hidden="true"></i></a>`)
                }
                else {
                  commentNavigationButtons.append(`<a class="btn btn-outline-dark" href="#${previousCommentThreadAnchor}" title="Previous Comment"><i class="fa fa-chevron-up" aria-hidden="true"></i></a>`)
                  commentNavigationButtons.append(`<a class="btn btn-outline-dark ml-1" href="#${nextCommentThreadAnchor}" title="Next Comment"><i class="fa fa-chevron-down" aria-hidden="true"></i></a>`)
                }
            }
        });
    }
});
