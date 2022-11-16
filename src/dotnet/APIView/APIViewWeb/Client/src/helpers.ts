// Updated Page Setting by Updating UserPreference 
export function updatePageSettings(callBack) {
  var hideLineNumbers = $("#hide-line-numbers").prop("checked");
  var hideLeftNavigation = $("#hide-left-navigation").prop("checked");
  var showHiddenApis = $("#show-hidden-api-checkbox").prop("checked");
  var uri = location.origin + `/userprofile/updatereviewpagesettings?hideLineNumbers=${hideLineNumbers}&hideLeftNavigation=${hideLeftNavigation}&showHiddenApis=${showHiddenApis}`;
    
  $.ajax({
    type: "PUT",
    url: uri
  }).done(callBack());
}
