/**
* Call APIView controller endpoint (/userprofile/updatereviewpagesettings)
* to update various page settings
* Takes a call back function that is run after ajax call succeeds
* @param { function } a callback function
*/
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

/**
* Retrieves a codeLineRow using the id
* @param { string } row id
*/
export function getCodeRow(id: string) {
  return $(`.code-line[data-line-id='${id}']`);
}

/**
* Retrieves the classList for a codeLineRow using the id
* @param { string } row id
*/
export function getCodeRowSectionClasses(id: string) {
  var codeRow = getCodeRow(id);
  var rowSectionClasses = "";
  if (codeRow) {
    rowSectionClasses = getRowSectionClasses(codeRow[0].classList);
  }
  return rowSectionClasses;
}

/**
* Retrieves the classes that identifies the codeLine as a section
* @param { DOMTokenList } classlist
*/
export function getRowSectionClasses(classList: DOMTokenList) {
  const rowSectionClasses: string[] = [];
  for (const value of classList.values()) {
    if (value == "section-loaded" || value.startsWith("code-line-section-content") || value.match(/lvl_[0-9]+_(parent|child)_[0-9]+/)) {
      rowSectionClasses.push(value);
    }
  }
  return rowSectionClasses.join(' ');
}

/**
* Updates the state of the comment icon (visible / invisible)
* @param { string } id
* @param { boolean } show
*/
export function toggleCommentIcon(id: string, show: boolean) {
  getCodeRow(id).find(".icon-comments").toggleClass("invisible", !show);
}

/**
* Retrieve a Specific Cookie from Users Browser
* @param { String } cookies (pass document.cookies)
* @param { String } cookieName
* @return { String } cookieValue
*/
export function getCookieValue (cookies: string, cookieName: string)
{
  const nameEQ = `${cookieName}=`;
  const charArr = cookies.split(';');
  for (let i = 0; i < charArr.length; i++)
  {
    let ch = charArr[i];
    while(ch.charAt(0) === ' ')
    {
      ch = ch.substring(1, ch.length);
    }
    if (ch.indexOf(nameEQ) === 0)
      return ch.substring(nameEQ.length, ch.length);    
  }
  return null;
}

/**
* Retrieve the list of classes on an element
* @param { JQuery<HTMLElement> | HTMLElement } element
* @return { string [] } classList - list of classes of the element
*/
export function getElementClassList (element : JQuery<HTMLElement> | HTMLElement) {
  let el : HTMLElement = (element instanceof HTMLElement) ? element : element[0];
  return Array.from(el.classList);
}

// ToastNotification
export enum NotificationLevel { info, warning, error }
export interface Notification {
  message : string;
  level : NotificationLevel
}

/**
* Contruct and add a toast notification to the page
* @param { ToastNotification } notification
* @param { number } duration - how long should the notification stay on the page
*/
export function addToastNotification(notification : Notification, id : string = "", duration : number = 10000) {
  const newtoast = $('#notification-toast').clone().removeAttr("id").attr("data-bs-delay", duration);
  if (id != "")
  {
    newtoast.attr("id", id);
  }
  
  switch (notification.level) {
    case 0:
      newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-circle-info text-info me-1" ></i>`);
      newtoast.find(".toast-header strong").html("Information");
      break;
    case 1:
      newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-triangle-exclamation text-warning me-1"></i>`);
      newtoast.find(".toast-header strong").html("Warning");
      break;
    case 2:
      newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-circle-exclamation text-danger me-1"></i>`);
      newtoast.find(".toast-header strong").html("Error");
      break;
  }
  newtoast.find(".toast-body").html(notification.message);
  const toastBootstrap = bootstrap.Toast.getOrCreateInstance(newtoast[0]);
  $("#notification-container").append(newtoast);
  toastBootstrap.show();
}