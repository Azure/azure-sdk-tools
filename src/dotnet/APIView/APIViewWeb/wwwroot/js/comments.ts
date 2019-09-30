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
        let commentRow = getCommentBox(lineId);
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
        getCommentBox(id).find(".comment-holder").toggle();
    }

    function getCommentBox(id) {
        return $(`.comment-box[data-line-id='${id}']`);
    }

    function hideCommentBox(id) {
        var thisRow = $(document.getElementById(id)).parents(".code-line").first();
        let diagnosticsRow = thisRow.next();
        let nextRow = diagnosticsRow.next();
        nextRow.find(".review-thread-reply").show();
        nextRow.find(".comment-form").hide();
    }

    function showCommentBox(id) {
        let thisRow = $(document.getElementById(id)).parents(".code-line").first();
        let diagnosticsRow = thisRow.next();
        let nextRow = diagnosticsRow.next();
        let commentBox = nextRow.find(".comment-form");

        if (commentBox.length === 0) {
            commentBox = commentFormTemplate.children().clone();

            var thread = nextRow.find(".comment-thread-contents");
            if (thread.length > 0) {
                thread.after(commentBox);
            }
            else {
                commentBox.insertAfter(diagnosticsRow).wrap(`<tr class="comment-box" data-line-id="${id}">`).wrap("<td colspan=\"2\">");
            }
        }

        commentBox.show();
        commentBox.find(".elementIdInput").val(id);
        commentBox.find(".new-thread-comment-text").focus();
        nextRow.find(".review-thread-reply").hide();
        return false;
    }

    function updateCommentThread(commentBox, partialViewResult) {
        partialViewResult = $.parseHTML(partialViewResult);
        $(commentBox).replaceWith(partialViewResult);
        return false;
    }
});
