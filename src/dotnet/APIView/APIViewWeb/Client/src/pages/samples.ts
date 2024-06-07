import * as hp from "../shared/helpers";
import { rightOffCanvasNavToggle } from "../shared/off-canvas";

$(() => {
  /* RIGHT OFFCANVAS OPERATIONS
--------------------------------------------------------------------------------------------------------------------------------------------------------*/
  // Open / Close right Offcanvas Menu
  $("#samples-right-offcanvas-toggle").on('click', function () {
    hp.updatePageSettings(function () {
      rightOffCanvasNavToggle("samples-main-container");
    });
  });
});
