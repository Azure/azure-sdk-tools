$(() => {
    let commentFormTemplate = $("#comment-form-template");

    $(document).on("click", ".commentable", e => {
        showCommentBox(e.target.id);
        e.preventDefault();
    });

    $(document).on("click", ".line-comment-button", e => {
        showCommentBox(getLineId(e.target));
        e.preventDefault();
    });

    $(document).on("click", ".comment-cancel-button", e => {
        hideCommentBox(getLineId(e.target));
        e.preventDefault();
    });

    $(document).on("click", "[data-post-update='comments']", e => {
        const form = <HTMLFormElement><any>$(e.target).closest("form");
        let lineId = getLineId(e.target);
        let commentRow = getCommentsRow(lineId);
        let serializedForm = form.serializeArray();
        serializedForm.push({ name: "lineId", value: lineId });

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
        showCommentBox(getLineId(e.target));
        e.preventDefault();
    });

    $(document).on("click", ".toggle-comments", e => {
        toggleComments(getLineId(e.target));
        e.preventDefault();
    });

    function getLineId(element) {
        return $(element).closest("[data-line-id]").data("line-id");
    }

    function toggleComments(id) {
        $(getCommentsRow(id)).find(".comment-holder").toggle();
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
            commentForm = createCommentForm(id);
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
                commentForm = $(createCommentForm(id)).insertAfter(reply);
            }

            reply.hide();
            commentForm.show();
        }

        commentForm.find(".new-thread-comment-text").focus();
    }

    function createCommentForm(id) {

        let form = commentFormTemplate.children().clone();
        form.find(".elementIdInput").val(id);
        return form;
    }

    function updateCommentThread(commentBox, partialViewResult) {
        partialViewResult = $.parseHTML(partialViewResult);
        $(commentBox).replaceWith(partialViewResult);
        return false;
    }
});
