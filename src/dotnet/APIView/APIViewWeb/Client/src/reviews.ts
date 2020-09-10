$(() => {
  const searchBox = $("#searchBox");

  // make the search box the initial focused element so users can just start typing once page loads
  searchBox.focus();

  const context = $(".review-name");
  searchBox.keyup(function () {

    // highlight matching text using mark.js framework
    const searchText = (searchBox.val() as string).toUpperCase();
    (context as any).unmark();
    if (searchText) {
      (context as any).mark(searchText, {
        done: function () {
          $(this).not(":has(mark)").hide();
        }
      });
    }

    // hide rows that do not match
    context.each(function () {
      $(this).closest("tr").toggle($(this)[0].innerText.toUpperCase().indexOf(searchText) > -1);
    });
  });
});
