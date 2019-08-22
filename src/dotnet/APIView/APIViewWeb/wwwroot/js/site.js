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

        $(element).find(".comment-cancel-button").click(function () {
            hideCommentBox(id);
            return false;
        });
        $(element).find(".comment-submit-button").off().click(function () {
            $.ajax({
                type: "POST",
                data: element.find("form").serialize()
            }).done(function (partialViewResult) {
                updateCommentThread(thisRow.next(), partialViewResult);
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
        attachEventHandlers(commentForm, id);
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
