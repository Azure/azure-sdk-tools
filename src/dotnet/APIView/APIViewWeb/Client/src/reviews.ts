//$(() => {
//  const searchBox = $("#searchBox");
//  const context = $(".review-name") as any;
//
//  // if already populated from navigating back, filter again
//  if (searchBox.val()) {
//    filter();
//  }
//  
//  searchBox.on("input", function () {
//    setTimeout(filter, 300);
//  });
//
//  function filter() {
//    // highlight matching text using mark.js framework and hide rows that don't match
//    const searchText = (searchBox.val() as string).toUpperCase();
//    context.closest("tr").show().unmark();
//    if (searchText) {
//      context.mark(searchText, {
//        done: function () {
//          context.not(":has(mark)").closest("tr").hide();
//        }
//      });
//    }
//  }
//});

// Add DataTables
$(() => {
    const languageSelect = $("#reviews-table-language-filter");
    const searchBox = $("#reviews-table-search-box");

    var reviewsTable = (<any>$('#reviews-table')).DataTable({
      "rowGroup": {
        dataSrc: 0
      },
      "dom": 't<"pl-2 pr-2"ipr>',
      "columnDefs" : [
        { orderable: false, targets: 5 }
      ],
      "pageLength" : 25,
      "search.smart" : true,
      "drawCallback": function (settings){
        // Enable bootstraps tooltip
        (<any>$('[data-toggle="tooltip"]')).tooltip();
      }
    });

    searchBox.on("input", function (e) {
      reviewsTable.search($(this).val() as string).draw();
    });

    languageSelect.on('change', function(e) {
      reviewsTable.columns(4).search($(this).val() as string, true, false, false).draw();
    });
  }
);


