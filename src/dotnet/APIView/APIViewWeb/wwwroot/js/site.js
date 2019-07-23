// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
$(function () {
    $(".commentable").click(function () {
        var formExists = $($(this).parents(".code-line")[0].nextElementSibling).find("#comment-form").length > 0;

        if (!formExists) {
            let myForm = $("#comment-form");
            let clone = myForm.clone();
            clone[0].querySelector("#id-box").setAttribute("value", this.id);
            let stringRep = clone[0].outerHTML;

            var thread = $($(this).parents(".code-line")[0].nextElementSibling).find(".comment-thread-contents");
            if (thread.length > 0) {
                thread.after(stringRep);
                $($(this).parents(".code-line")[0].nextElementSibling).find(".review-thread-reply").hide();
            }
            else {
                $(this).parents(".code-line").after("<tr><td>" + stringRep + "</td></tr>");
            }
        }
        $($(this).parents(".code-line")[0].nextElementSibling).find(".comment").show();
        $($(this).parents(".code-line")[0].nextElementSibling).find("#new-thread-comment-text").focus();
        return false;
    });
});

$(function () {
    $(".review-thread-reply-button").click(function () {
        $($(this).parents(".comment-box")[0]).prev()[0].querySelector(".commentable").click();
    });
});

$(function () {
    $("#cancel-button").click(function () {
        $($($(this).parents(".comment-box")[0]).querySelector("#comment-form")).remove();
        $($(this).parents(".comment-box")[0]).querySelector(".review-thread-reply").show();
    });
});

/*
<form id="comment-form" class="comment" method="post" asp-route-id="@Model.Id">
    <div class="form-group">
        <label>ID:</label>
        <input type="text" readonly id="id-box" asp-for="Comment.ElementId" class="form-control" />
    </div>
    <div class="form-group">
        <label>Comment:</label>
        <textarea id="comment-thread" asp-for="Comment.Comment" class="form-control" rows="3"></textarea>
    </div>
    <button type="submit" id="submit-button" class="btn btn-outline-dark">Add Comment</button>
</form>
*/
