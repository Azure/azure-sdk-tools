// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
$(function () {
    let commentFormTemplate = $("#comment-form-template");

    function hideCommentBox(id) {
        var thisRow = $(document.getElementById(id)).parents(".code-line").first();
        var nextRow = thisRow.next();
        nextRow.find(".review-thread-reply").show();
        nextRow.find(".comment-form").hide();
    }

    function showCommentBox(id) {
        var thisRow = $(document.getElementById(id)).parents(".code-line").first();
        var nextRow = thisRow.next();
        var commentForm = nextRow.find(".comment-form");

        if (commentForm.length == 0) {
            commentForm = commentFormTemplate.children().clone();

            var thread = nextRow.find(".comment-thread-contents");
            if (thread.length > 0) {
                thread.after(commentForm);
            }
            else {
                commentForm.insertAfter(thisRow).wrap("<tr>").wrap("<td>");
            }
        }

        commentForm.show();
        commentForm.find(".id-box").val(id);
        commentForm.find(".new-thread-comment-text").focus();
        commentForm.find(".comment-cancel-button").click(function () { hideCommentBox(id); });

        nextRow.find(".review-thread-reply").hide();
        return false;
    }

    $(".commentable").click(function () {
        showCommentBox(this.id);
    });

    $(".review-thread-reply-button").click(function () {
        showCommentBox($(this).data("element-id"));
    });
});
