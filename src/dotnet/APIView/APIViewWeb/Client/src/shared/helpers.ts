// Updated Page Setting by Updating UserPreference 
export function updatePageSettings(callBack) {
  var hideLineNumbers = false;//$("#hide-line-numbers").prop("checked");
  var hideLeftNavigation = $("#hide-left-navigation").prop("checked");
  var showHiddenApis = false;//$("#show-hidden-api-checkbox").prop("checked");
  var hideReviewPageOptions = !$("#review-right-offcanvas-toggle").prop("checked");
  var hideIndexPageOptions = !$("#index-right-offcanvas-toggle").prop("checked");
  var uri = location.origin + `/userprofile/updatereviewpagesettings?` +
                              `hideLineNumbers=${hideLineNumbers}&` +
                              `hideLeftNavigation=${hideLeftNavigation}&` +
                              `showHiddenApis=${showHiddenApis}&` +
                              `hideReviewPageOptions=${hideReviewPageOptions}&` +
                              `hideIndexPageOptions=${hideIndexPageOptions}`;

  $.ajax({
    type: "PUT",
    url: uri
  }).done(callBack());
}
