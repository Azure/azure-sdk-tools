// Updated Page Setting by Updating UserPreference 
export function updatePageSettings(callBack) {
  var hideLineNumbers = $("#hide-line-numbers").prop("checked");
  if (hideLineNumbers != undefined) { hideLineNumbers = !hideLineNumbers; }

  var hideLeftNavigation = $("#hide-left-navigation").prop("checked");
  if (hideLeftNavigation != undefined) { hideLeftNavigation = !hideLeftNavigation; }

  var showHiddenApis = $("#show-hidden-api-checkbox").prop("checked");
  var showComments = $("#show-comments-checkbox").prop("checked");
  var showSystemComments = $("#show-system-comments-checkbox").prop("checked");

  var hideReviewPageOptions = $("#review-right-offcanvas-toggle").prop("checked");
  if (hideReviewPageOptions != undefined) { hideReviewPageOptions = !hideReviewPageOptions; }

  var hideIndexPageOptions = $("#index-right-offcanvas-toggle").prop("checked");
  if (hideIndexPageOptions != undefined) { hideIndexPageOptions = !hideIndexPageOptions; }

  var uri = location.origin + `/userprofile/updatereviewpagesettings?` +
                              `hideLineNumbers=${hideLineNumbers}&` +
                              `hideLeftNavigation=${hideLeftNavigation}&` +
                              `showHiddenApis=${showHiddenApis}&` +
                              `hideReviewPageOptions=${hideReviewPageOptions}&` +
                              `hideIndexPageOptions=${hideIndexPageOptions}&` +
                              `showComments=${showComments}&` +
                              `showSystemComments=${showSystemComments}`;

  $.ajax({
    type: "PUT",
    url: uri
  }).done(callBack());
}
