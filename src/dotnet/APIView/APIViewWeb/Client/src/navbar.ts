import Split from "split.js";
import { updatePageSettings } from "./helpers";

addEventListener("load", () => {
    $(".nav-list-toggle").click(function () {
        $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    });
});

$(() => {
    const themeSelector = $( '#theme-selector' );

    // Add EventListener for Changing CSS Theme
    themeSelector.on('change', function() {
        updatePageSettings(function(){
            var allThemes = themeSelector.children();
            var newTheme = themeSelector.children(":selected").val() as string;
            var themesToRemove = allThemes.filter(function(){
                return ($(this).val() as string) != newTheme;
            });
            var body = $('body');
    
            themesToRemove.each(function(){
                body.removeClass(($(this).val() as string));
            })
            body.addClass(newTheme);
        });
    });
});
