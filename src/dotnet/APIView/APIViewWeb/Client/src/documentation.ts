$(() => {
  const SEL_DOC_CLASS = ".documentation";

  $(document).on("click", "#show-documentation-checkbox", e => {
    toggleAllDocumentationVisibility(e.target.checked);
  });

  function toggleAllDocumentationVisibility(showDocumentation: boolean) {
    $(SEL_DOC_CLASS).toggle(showDocumentation);
  }

});
