import { updatePageSettings } from "./helpers";

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
