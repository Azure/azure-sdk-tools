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
        let thisRow = $(document.getElementById(id)).parents(".code-line").first();
        let nextRow = thisRow.next();
        let commentForm = nextRow.find(".comment-form");

        if (commentForm.length == 0) {
            commentForm = commentFormTemplate.children().clone();

            var thread = nextRow.find(".comment-thread-contents");
            if (thread.length > 0) {
                thread.after(commentForm);
            }
            else {
                commentForm.insertAfter(thisRow).wrap("<tr>").wrap("<td colspan=\"2\">");
            }
        }

        commentForm.show();
        commentForm.find(".id-box").val(id);
        commentForm.find(".new-thread-comment-text").focus();
        commentForm.find(".comment-cancel-button").click(function () { hideCommentBox(id); });
        commentForm.find(".comment-submit-button").click(function () {
            $.ajax({
                type: "POST",
                data: commentForm.find("form").serialize()
            }).done(function (partialViewResult) {
                updateCommentThread(thisRow.next(), partialViewResult);
            });
            return false;
        });

        nextRow.find(".review-thread-reply").hide();
    }

    function updateCommentThread(commentBox, partialViewResult) {
        partialViewResult = $.parseHTML(partialViewResult);
        if ($(partialViewResult).find(".review-comment").length === 0) {
            $(commentBox).remove();
        } else {
            $(commentBox).replaceWith(partialViewResult);
            $(partialViewResult).find(".review-thread-reply-button").click(function () {
                showCommentBox($(this).data("element-id"));
            });
            $(partialViewResult).find(".comment-delete-button-enabled").click(function () {
                deleteComment(this.id);
                return false;
            });
        }
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

    $(".comment-delete-button-enabled").click(function () {
        deleteComment(this.id);
        return false;
    });

    $(".commentable").click(function () {
        showCommentBox(this.id);
        return false;
    });

    $(".review-thread-reply-button").click(function () {
        showCommentBox($(this).data("element-id"));
    });

    $(".code-line").hover(function () {
        var button = $(this).find(".line-comment-button");
        button.toggleClass("is-hovered");
    });

    $(".line-comment-button").click(function () {
        showCommentBox($(this).data("element-id"));
        return false;
    });
});
