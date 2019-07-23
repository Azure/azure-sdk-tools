// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
$(function () {
    $(".commentable").click(function () {
        var nextRow = $(this).parents(".code-line").first().next();
        var formExists = nextRow.find(".comment-form").length > 0;

        if (!formExists) {
            let myForm = $(".comment-form");
            let clone = myForm.clone();
            clone.first().find(".id-box").val(this.id);
            let stringRep = clone[0].outerHTML;

            var thread = nextRow.find(".comment-thread-contents");
            if (thread.length > 0) {
                thread.after(stringRep);
                nextRow.find(".review-thread-reply").hide();
            }
            else {
                $(this).parents(".code-line").after("<tr><td>" + stringRep + "</td></tr>");
                nextRow = $(this).parents(".code-line").first().next();
            }
        }
        nextRow.find(".comment").show();
        nextRow.find(".new-thread-comment-text").focus();
        return false;
    });
});

$(function () {
    $(".review-thread-reply-button").click(function () {
        $(this).parents(".comment-box").first().prev().first().find(".commentable").click();
    });
});
/*
$(function () {
    $("#cancel-button").click(function () {
        $($(this).parents(".comment-box")[0].querySelector("#comment-form")).remove();
        $($(this).parents(".comment-box")[0].querySelector(".review-thread-reply")).show();
    });
});
*/
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
