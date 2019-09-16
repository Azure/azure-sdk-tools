// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
$(function () {
    let commentFormTemplate = $("#comment-form-template");
    attachEventHandlers(document);

    $(document).find(".nav-list-toggle").click(function() {
        $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    });

    $(document).find(".commentable").click(function () {
        showCommentBox(this.id);
        return false;
    });

    $(document).find(".line-comment-button").click(function () {
        showCommentBox($(this).data("element-id"));
        return false;
    });

    function attachEventHandlers(element, id=null) {
        let thisRow = $(document.getElementById(id)).parents(".code-line").first();
        let diagnosticsRow = thisRow.next();
        let commentsRow = diagnosticsRow.next();

        $(element).find(".comment-cancel-button").click(function () {
            hideCommentBox(id);
            return false;
        });
        $(element).find(".comment-submit-button").off().click(function () {
            $.ajax({
                type: "POST",
                data: element.find("form").serialize()
            }).done(function (partialViewResult) {
                updateCommentThread(commentsRow, partialViewResult);
            });
            return false;
        });

        $(element).find(".review-thread-reply-button").click(function () {
            showCommentBox($(this).data("element-id"));
        });

        $(element).find(".comment-delete-button-enabled").click(function () {
            deleteComment(this.id);
            return false;
        });
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

        if (commentBox.length == 0) {
            commentBox = commentFormTemplate.children().clone();

            var thread = nextRow.find(".comment-thread-contents");
            if (thread.length > 0) {
                thread.after(commentBox);
            }
            else {
                commentBox.insertAfter(diagnosticsRow).wrap("<tr>").wrap("<td colspan=\"2\">");
            }
        }

        commentBox.show();
        commentBox.find(".id-box").val(id);
        commentBox.find(".new-thread-comment-text").focus();
        attachEventHandlers(commentBox, id);
        nextRow.find(".review-thread-reply").hide();
        return false;
    }

    function updateCommentThread(commentBox, partialViewResult) {
        partialViewResult = $.parseHTML(partialViewResult);
        $(commentBox).replaceWith(partialViewResult);
        attachEventHandlers(partialViewResult);
        return false;
    }

    function deleteComment(id) {
        let button = document.getElementById(id);
        let commentBox = $(button).parents(".comment-box").first();
        $.ajax({
            type: "POST",
            url: "?handler=delete",
            data: $(button).parents("form").serialize()
        }).done(function (partialViewResult) {
            updateCommentThread(commentBox, partialViewResult);
        });
    }
});
