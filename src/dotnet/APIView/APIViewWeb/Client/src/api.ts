$(() => {
  $(document).on("click", "#hide-line-numbers", e => {
    $(".line-number").toggleClass("line-number-hidden", e.target.checked);
  });
});
