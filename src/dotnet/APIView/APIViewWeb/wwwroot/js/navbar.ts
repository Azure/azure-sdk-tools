addEventListener("load", () => {
    $(".nav-list-toggle").click(function () {
        $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    });
});