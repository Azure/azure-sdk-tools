// Updated Page Setting by Updating UserPreference 
export function updatePageSettings(callBack) {
  var hideLineNumbers = $("#hide-line-numbers").prop("checked");
  var hideLeftNavigation = $("#hide-left-navigation").prop("checked");
  var selectedTheme = $("#theme-selector").children(":selected").val() as string;
  var uri = location.origin + `/account/updatesettings?hideLineNumbers=${hideLineNumbers}&hideLeftNavigation=${hideLeftNavigation}&theme=${selectedTheme}`;
    
  $.ajax({
    type: "PUT",
    url: uri
  }).done(callBack());
}