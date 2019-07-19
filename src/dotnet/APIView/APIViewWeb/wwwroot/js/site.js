// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
$(function () {
    $(".commentable").click(function () {
        var myForm = $("#comment-form");
        $("#id-box").val(this.id);
        myForm.show();
        $("#comment-thread").focus();
        return false;
    });
});
