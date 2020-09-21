$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";

  hideCheckboxIfNoDocs();

  function hideCheckboxIfNoDocs() {
      if ($(SEL_DOC_CLASS).length == 0) {
          $(SHOW_DOC_CHECK_COMPONENT).hide();
      }
  }
});
