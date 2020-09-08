$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECKBOX = "#show-documentation-checkbox";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SEL_CODE_INNER_CLASS = ".code-inner";
  const SEL_CODE_LINE = ".code-line";

  hideCheckboxIfNoDocs();
  toggleEmptyLineVisibility(false);

  $(document).on("click", SHOW_DOC_CHECKBOX, e => {
      toggleAllDocumentationVisibility(e.target.checked);
  });

  function hideCheckboxIfNoDocs() {
      if ($(SEL_DOC_CLASS).length == 0) {
          $(SHOW_DOC_CHECK_COMPONENT).hide();
      }
  }

  function toggleEmptyLineVisibility(showDocuments: boolean) {
      //If code line only has documentation then toggle that line
      $(SEL_CODE_LINE).each(function () {
          var tokenElements = $(this).find(SEL_CODE_INNER_CLASS).children();
          //Checking atleast one doc node is present to avoid hiding explicit empty lines
          if (tokenElements.filter(SEL_DOC_CLASS).length > 0 && tokenElements.not(SEL_DOC_CLASS).length == 0) {
                $(this).toggle(showDocuments);
            }
      });
  }

  function toggleAllDocumentationVisibility(showDocuments: boolean) {
      $(SEL_DOC_CLASS).toggle(showDocuments);
      toggleEmptyLineVisibility(showDocuments);
  }
});
