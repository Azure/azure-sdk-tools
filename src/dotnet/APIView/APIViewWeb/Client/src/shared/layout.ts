// Enable tooltip and Popovers
declare const bootstrap: any;

const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
const tooltipList = (<any>[...tooltipTriggerList]).map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))

const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]')
const popoverList = (<any>[...popoverTriggerList]).map(popoverTriggerEl => new bootstrap.Popover(popoverTriggerEl))
