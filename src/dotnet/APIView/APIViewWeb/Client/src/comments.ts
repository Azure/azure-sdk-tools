$(() => {
    const INVISIBLE = "invisible";
    const SEL_CODE_DIAG = ".code-diagnostics";
    const SEL_COMMENT_ICON = ".icon-comments";
    const SEL_COMMENT_CELL = ".comment-cell";

    let MessageIconAddedToDom = false;

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
        toggleAllCommentsAndDiagnosticsVisibility(e.target.checked);
    });

    $(document).on("click", SEL_COMMENT_ICON, e => {
        let lineId = getElementId(e.target);
        if (lineId) {
            toggleSingleCommentAndDiagnostics(lineId);
        }
        e.preventDefault();
    });

    $(document).on("click", "[data-post-update='comments']", e => {
        const form = <HTMLFormElement><any>$(e.target).closest("form");
        let lineId = getElementId(e.target);
        if (lineId) {
            let commentRow = getCommentsRow(lineId);
            let serializedForm = form.serializeArray();
            serializedForm.push({ name: "elementId", value: lineId });
            serializedForm.push({ name: "reviewId", value: getReviewId(e.target) });
            serializedForm.push({ name: "revisionId", value: getRevisionId(e.target) });

            $.ajax({
                type: "POST",
                url: $(form).prop("action"),
                data: $.param(serializedForm)
            }).done(partialViewResult => {
                updateCommentThread(commentRow, partialViewResult);
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
        let lineId = getElementId(e.target);
        if (lineId) {
            editComment(lineId);
        }
        e.preventDefault();
    });


    function getReviewId(element: HTMLElement) {
        return getParentData(element, "data-review-id");
    }

    function getRevisionId(element: HTMLElement) {
        return getParentData(element, "data-revision-id");
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
                    .append($("<td colspan=\"2\">")
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

    function toggleAllCommentsAndDiagnosticsVisibility(showComments: boolean) {
        $(SEL_COMMENT_CELL + ", " + SEL_CODE_DIAG).each(function () {
            var id = getElementId(this);
            if (id) {
                getCommentsRow(id).toggle(showComments);
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
            $(".line-comment-button-cell").append(`<span class="icon icon-comments ` + INVISIBLE + `">💬</span>`);
            MessageIconAddedToDom = true;
        }
    }

    function toggleCommentIcon(id, show: boolean) {
        getCodeRow(id).find(SEL_COMMENT_ICON).toggleClass(INVISIBLE, !show);
    }
});
