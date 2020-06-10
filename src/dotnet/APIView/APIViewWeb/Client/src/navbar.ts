import Split from "split.js";

addEventListener("load", () => {
    $(".nav-list-toggle").click(function () {
        $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    });
});

$(() => {
    /* 992px matches bootstrap col-lg min-width */
    ($('.namespace-view') as any).stickySidebar({ minWidth: 992 });   

    /* Split left and right review panes using split.js */
    const rl = $('#review-left');
    const rr = $('#review-right');

    if (rl.length && rr.length) {
        Split(['#review-left', '#review-right'], {
            direction: 'horizontal',
            sizes: [17, 83],
            elementStyle: (dimension, size, gutterSize) => {
                return {
                    'flex-basis': `calc(${size}% - ${gutterSize}px`
                }
            },
            gutterStyle: (dimension, gutterSize) => {
                return {
                    'flex-basis': `${gutterSize}px`
                }
            }
        });
    }
});
