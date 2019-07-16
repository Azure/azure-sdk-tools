// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
$(function () {
    $(".commentable").click(function () {
        // get the comment form
        var myForm = $("#commentForm");

        var idField = document.getElementById("idBox");
        idField.innerHTML = this.id;

        // get the current value of the form's display property
        myForm.show();
    });
});
