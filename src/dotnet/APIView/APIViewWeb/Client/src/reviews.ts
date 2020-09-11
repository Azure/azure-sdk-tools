$(() => {
  const searchBox = $("#searchBox");

  // clear the filter so we don't have to deal with the case of a user navigating backwards and
  // the reviews not matching. TODO it would be nice to handle this case on the server at some point.
  searchBox.val("");

  // make the search box the initial focused element so users can just start typing once page loads
  searchBox.focus();

  const context = $(".review-name") as any;
  searchBox.on("input", function () {
    setTimeout(function () {
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
    },
      // use a timeout to prevent bad perf for holding down backspace or really fast typing
      200);
  });
});
