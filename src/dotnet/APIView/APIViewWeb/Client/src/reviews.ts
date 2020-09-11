$(() => {
  const searchBox = $("#searchBox");
  const context = $(".review-name") as any;

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
});
