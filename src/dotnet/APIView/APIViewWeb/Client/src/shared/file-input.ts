addEventListener("load", () => {
    // Show file name when file is selected
    $(".custom-file-input").on("change", function() {
        const fileName = (<HTMLInputElement>this).files![0].name;
        $(this).next(".custom-file-label").html(fileName);
    });
});