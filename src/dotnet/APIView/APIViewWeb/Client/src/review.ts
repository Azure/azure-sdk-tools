$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SHOW_DOC_CHECKBOX = ".show-doc-checkbox";
  const SHOW_DOC_HREF = ".show-document";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly";
  const HIDE_LINE_NUMBERS = "#hide-line-numbers";

  hideCheckboxIfNoDocs();

  function hideCheckboxIfNoDocs() {
      if ($(SEL_DOC_CLASS).length == 0) {
          $(SHOW_DOC_CHECK_COMPONENT).hide();
      }
  }

  $(SHOW_DOC_CHECKBOX).on("click", e => {
    $(SHOW_DOC_HREF)[0].click();
  });

  $(SHOW_DIFFONLY_CHECKBOX).on("click", e => {
    $(SHOW_DIFFONLY_HREF)[0].click();
  });

  $(HIDE_LINE_NUMBERS).on("click", e => {
    $(".line-number").toggleClass("d-none");
  });

  // Diff button
  $('.diff-button').each(function(index, value){
    $(this).on('click', function () {
      window.location.href = $(this).val() as string;
    });
  });

  // Change dropdown filter for review and revision
  $('#revisions-bootstraps-select, #review-bootstraps-select, #diff-bootstraps-select').each(function(index, value) {
    $(this).on('change', function() {
      var url = $(this).find(":selected").val();
      if (url)
      {
        window.location.href = url as string;
      }
    });
  });

  $('.row-fold-elipsis, .row-fold-caret').on('click', function() {
    var parentRow = $(this).parents('.code-line');
    var parentRowClasses = parentRow.attr('class');
    if (parentRowClasses) {
      var foldableClassPrefix = parentRowClasses.split(' ').filter(c => c.endsWith('-parent'))[0].replace("-parent","");
      $(`.${foldableClassPrefix}-content`).toggleClass("d-none");
    }
    parentRow.find(".row-fold-elipsis").toggleClass("d-none");
    var caretIcon = parentRow.find(".row-fold-caret").children("i");
    var caretClasses =  caretIcon.attr("class");
    if (caretClasses)
    {
      var caretDirection = caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0];
      if (caretDirection.endsWith("right"))
      {
        caretIcon.removeClass("fa-angle-right");
        caretIcon.addClass("fa-angle-down");
      }
      else
      {
        caretIcon.removeClass("fa-angle-down");
        caretIcon.addClass("fa-angle-right");
      }
    }
  });

  // enable tooltip and popover
  (<any>$('[data-toggle="tooltip"]')).tooltip();
  (<any>$('[data-toggle="popover"]')).popover();
});