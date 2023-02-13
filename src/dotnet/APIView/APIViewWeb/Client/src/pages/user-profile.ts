import { updatePageSettings } from "../shared/helpers";

$(() => {
  const themeSelector = $('#theme-selector');
  const approvableLangSelect = $('#approvable-language-select');

  (<any>themeSelector).SumoSelect();
  (<any>approvableLangSelect).SumoSelect({selectAll: true});

  $(document).on("submit", "form[data-post-update='userProfile']", e => {
    const form = <HTMLFormElement><any>$(e.target);
    
    let serializedForm = form.serializeArray();
    $(form).find("input[type='submit']").attr('disabled', 'disabled');
    $.ajax({
      type: "POST",
      url: $(form).prop("action"),
      data: $.param(serializedForm)
    }).done(res => {
      $(form).find("input[type='submit']").removeAttr('disabled');
    });
    e.preventDefault();
  });
  
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
