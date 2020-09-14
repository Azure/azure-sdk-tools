$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECKBOX = "#show-documentation-checkbox";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";

  hideCheckboxIfNoDocs();
 
  $(document).on("click", SHOW_DOC_CHECKBOX, e => {
      $(SEL_DOC_CLASS).toggle(e.target.checked);
  });

  function hideCheckboxIfNoDocs() {
      if ($(SEL_DOC_CLASS).length == 0) {
          $(SHOW_DOC_CHECK_COMPONENT).hide();
      }
  }
});
