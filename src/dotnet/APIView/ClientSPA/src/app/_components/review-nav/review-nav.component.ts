import { Component } from '@angular/core';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-review-nav',
  templateUrl: './review-nav.component.html',
  styleUrls: ['./review-nav.component.scss']
})
export class ReviewNavComponent {
  menuItems : MenuItem[] | undefined = [];
  activeItem: MenuItem | undefined;

  ngOnInit() {
    this.menuItems = this.setDockItems();
    this.activeItem = this.menuItems[0];
  }

  setDockItems() {
    return [
      {
        label: 'API',
        icon: 'bi bi-braces',
        expanded: false
      },
      {
        label: 'Revisions',
        icon: 'bi bi-clock-history',
        expanded: true
      },
      {
        label: 'Conversiation',
        icon: 'fa-regular fa-message',
        expanded: true
      },
      {
        label: 'Samples',
        icon: 'bi bi-puzzle',
        expanded: true
      },
    ]
  }

}
