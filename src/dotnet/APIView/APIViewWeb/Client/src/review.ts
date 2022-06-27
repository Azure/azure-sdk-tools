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

  // Collapsible Code Lines
  $('.row-fold-elipsis, .row-fold-caret').on('click', function () {
    var triggeringClass = $(this)[0].className;
    var headingRow = $(this).parents('.code-line');
    var headingRowClasses = headingRow.attr('class');
    var caretIcon = headingRow.find(".row-fold-caret").children("i");
    var caretClasses = caretIcon.attr("class");
    var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
    var foldableClassPrefix = headingRowClasses ? headingRowClasses.split(' ').filter(c => c.endsWith('-heading'))[0].replace("-heading", "") : "";


    if (triggeringClass == "row-fold-caret" && caretDirection == "fa-angle-down") {
      var classesOfRowsToHide = [`${foldableClassPrefix}-content`];

      do
      {
        let rowToHide = classesOfRowsToHide.pop();
        if (rowToHide) {
          $(`.${rowToHide}`).each(function (index, value) {
            let contentRowClasses = $(this).attr("class");
            if (contentRowClasses) {
              let subHeadingRows = contentRowClasses.split(' ').filter(c => c.endsWith('-heading'));
              subHeadingRows.forEach(item => {
                classesOfRowsToHide.push(item.replace("-heading", "-content"));
              });
            }
            $(this).addClass("d-none");
          });
          let subHeadingClass = rowToHide.replace("-content", "-heading");
          let subHeadingRow = $(`.${subHeadingClass}`);
          let subHeadingCaretIcon = subHeadingRow.find(".row-fold-caret").children("i");
          let subHeadingCaretClasses = subHeadingCaretIcon.attr("class");
          let caretDirection = subHeadingCaretClasses ? subHeadingCaretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
          if (caretDirection) {
            subHeadingCaretIcon.removeClass("fa-angle-down");
            subHeadingCaretIcon.addClass("fa-angle-right");
          }
          subHeadingRow.find(".row-fold-elipsis").removeClass("d-none");
        }
      }
      while (classesOfRowsToHide.length > 0);
    }
    else {
      $(`.${foldableClassPrefix}-content`).toggleClass("d-none");
      // toggle ellipsis
      headingRow.find(".row-fold-elipsis").toggleClass("d-none");

      // Toggle caret direction
      if (caretDirection.endsWith("right")) {
        caretIcon.removeClass("fa-angle-right");
        caretIcon.addClass("fa-angle-down");
      }
      else {
        caretIcon.removeClass("fa-angle-down");
        caretIcon.addClass("fa-angle-right");
      }
    }
  });

  // enable tooltip and popover
  (<any>$('[data-toggle="tooltip"]')).tooltip();
  (<any>$('[data-toggle="popover"]')).popover();
});
