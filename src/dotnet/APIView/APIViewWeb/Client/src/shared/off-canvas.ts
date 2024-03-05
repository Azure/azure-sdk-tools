// Functions to Control right OffCanvas Menu
export function rightOffCanvasNavToggle(mainContainser: String) {
  if ($(".right-offcanvas").css("width") == '0px') {
    $(`#${mainContainser}`).addClass("move-main-content-container-left");
    $("#right-offcanvas-menu").addClass("show-right-offcanvas");
  }
  else {
    $("#right-offcanvas-menu").removeClass("show-right-offcanvas");
    $(`#${mainContainser}`).removeClass("move-main-content-container-left");
  }
}
export function leftOffCanvasNavToggle(mainContainser: String) {
  if ($(".left-offcanvas").css("width") == '0px') {
    $(`#${mainContainser}`).addClass("move-main-content-container-right");
    $("#left-offcanvas-menu").addClass("show-left-offcanvas");
  }
  else {
    $("#left-offcanvas-menu").removeClass("show-left-offcanvas");
    $(`#${mainContainser}`).removeClass("move-main-content-container-right");
  }
}

