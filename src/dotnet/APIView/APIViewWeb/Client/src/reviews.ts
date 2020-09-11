$(() => {
  const searchBox = $("#searchBox");
  const context = $(".review-name") as any;

  // make the search box the initial focused element so users can just start typing once page loads,
  // but only on the initial load
  searchBox.focus();

  // if already populated from navigating back, filter again
  if (searchBox.val()) {
    filter();
  }
  
  searchBox.on("input", function () {
    setTimeout(filter, 300);
  });

  function filter() {
    // highlight matching text using mark.js framework and hide rows that don't match
    const searchText = (searchBox.val() as string).toUpperCase();
    context.closest("tr").show().unmark();
    if (searchText) {
      context.mark(searchText, {
        done: function () {
          context.not(":has(mark)").closest("tr").hide();
        }
      });
    }
  }
  //$(window).on("beforeunload", e => {
  //  const searchText = (searchBox.val() as string).toUpperCase();
  //  if (searchText) {
  //    const form = <HTMLFormElement><any>$("#filter");
  //    let serializedForm = form.serializeArray();
  //    serializedForm.push({ name: "filter", value: searchText });
  //    $.ajax({
  //      type: "POST",
  //      url: form.prop("action"),
  //      data: $.param(serializedForm)
  //    });
  //  }
  //  //e.preventDefault();
  //});
});
