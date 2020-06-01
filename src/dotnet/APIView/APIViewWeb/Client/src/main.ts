import "./comments.ts";
import "./file-input.ts";
import "./navbar.ts";
import Split from "split.js";

// Split left and right review panes using split.js.
const rl = $('#review-left');
const rr = $('#review-right');

if (rl.length && rr.length) {
  Split(['#review-left', '#review-right'], {
    direction: 'horizontal',
    sizes: [17, 83],
  });
}
