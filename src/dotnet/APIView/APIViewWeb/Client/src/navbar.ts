addEventListener("load", () => {
    $(".nav-list-toggle").click(function () {
        $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    });
});
$(() => {
    /* 992px matches bootstrap col-lg min-width*/
    ($('.namespace-view') as any).stickySidebar({ minWidth: 992 });   
});