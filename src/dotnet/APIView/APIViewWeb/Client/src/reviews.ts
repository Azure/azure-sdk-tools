$(() => {
  const searchBox = $("#searchBox");

  // make the search box the initial focused element so users can just start typing once page loads
  searchBox.focus();

  const context = $(".review-name") as any;
  searchBox.keyup(function () {

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
  });
});
