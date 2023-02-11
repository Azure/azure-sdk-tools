// Functions to Control right OffCanvas Menu
export function rightOffCanvasNavToggle(mainContainser: String) {
  if ($(".right-offcanvas").css("width") == '0px') {
    $(`#${mainContainser}`).addClass("move-main-content-container-left");
    $("#right-offcanvas-menu").addClass("show-offcanvas");
  }
  else {
    $("#right-offcanvas-menu").removeClass("show-offcanvas");
    $(`#${mainContainser}`).removeClass("move-main-content-container-left");
  }
}
