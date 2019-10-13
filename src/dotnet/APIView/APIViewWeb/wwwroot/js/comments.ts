$(() => {
    $(document).on("click", ".commentable", e => {
        showCommentBox(e.target.id);
        e.preventDefault();
    });

    $(document).on("click", ".line-comment-button", e => {
        showCommentBox(getElementId(e.target));
        e.preventDefault();
    });

    $(document).on("click", ".comment-cancel-button", e => {
        hideCommentBox(getElementId(e.target));
        e.preventDefault();
    });

    $(document).on("click", "#show-comments-checkbox", e => {
        toggleCommentsDisplay(e.target);
    });

    $(document).on("click", "[data-post-update='comments']", e => {
        const form = <HTMLFormElement><any>$(e.target).closest("form");
        let lineId = getElementId(e.target);
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
        e.preventDefault();
    });

    $(document).on("click", ".review-thread-reply-button", e => {
        showCommentBox(getElementId(e.target));
        e.preventDefault();
    });

    $(document).on("click", ".toggle-comments", e => {
        toggleComments(getElementId(e.target));
        e.preventDefault();
    });

    $(document).on("click", ".js-edit-comment", e => {
        editComment(getCommentId(e.target));
        e.preventDefault();
    });


    function getReviewId(element) {
        return getParentData(element, "data-review-id");
    }

    function getRevisionId(element) {
        return getParentData(element, "data-revision-id");
    }

    function getElementId(element) {
        return getParentData(element, "data-line-id");
    }

    function getCommentId(element) {
        return getParentData(element, "data-comment-id");
    }

    function getParentData(element, name) {
        return $(element).closest(`[${name}]`).attr(name);
    }

    function toggleComments(id) {
        $(getCommentsRow(id)).find(".comment-holder").toggle();
    }

    function editComment(commentId) {
        let commentElement = $(getCommentElement(commentId));
        let commentText = commentElement.find(".js-comment-raw").html();
        let template = createCommentEditForm(commentId, commentText);
        commentElement.replaceWith(template);
    }

    function getCommentElement(commentId) {
        return $(`.review-comment[data-comment-id='${commentId}']`);
    }

    function getCommentsRow(id) {
        return $(`.comment-row[data-line-id='${id}']`);
    }

    function getCodeRow(id) {
        return $(`.code-line[data-line-id='${id}']`);
    }

    function getDiagnosticsRow(id) {
        return $(`.code-diagnostics[data-line-id='${id}']`);
    }

    function hideCommentBox(id) {
        let commentsRow = getCommentsRow(id);
        commentsRow.find(".review-thread-reply").show();
        commentsRow.find(".comment-form").hide();
    }

    function showCommentBox(id) {
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
        }

        commentForm.find(".new-thread-comment-text").focus();
    }

    function createCommentForm() {
        return $("#js-comment-form-template").children().clone();
    }

    function createCommentEditForm( commentId, text) {

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

    function toggleCommentsDisplay(element) {
        if (element.checked) {
            $(".comment-row").css("display", "");
            $(".code-diagnostics").css("display", "");
        }
        else {
            $(".comment-row").css("display", "none");
            $(".code-diagnostics").css("display", "none");
        }
    }
});
