import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Review } from 'src/app/_models/review';

@Component({
  selector: 'app-review-info',
  templateUrl: './review-info.component.html',
  styleUrls: ['./review-info.component.scss']
})
export class ReviewInfoComponent implements OnInit {
  @Input() review : Review | undefined = undefined;
  @Output() revisionsSidePanel : EventEmitter<boolean> = new EventEmitter<boolean>();

  breadCrumbItems: MenuItem[] | undefined;
  breadCrumbHome: MenuItem | undefined;
  badgeClass : Map<string, string> = new Map<string, string>();

  constructor() {
    // Set Badge Class for Icons
    this.badgeClass.set("Pending", "fa-solid fa-circle-minus text-warning");
    this.badgeClass.set("Approved", "fas fa-check-circle text-success");
    this.badgeClass.set("Manual", "fa-solid fa-arrow-up-from-bracket");
    this.badgeClass.set("PullRequest", "fa-solid fa-code-pull-request");
    this.badgeClass.set("Automatic", "fa-solid fa-robot");
  }

  ngOnInit() {
    this.breadCrumbItems = [{ label: 'Review' }, { label: 'Microsoft.Azure.Functions.Worker.Extensions.ServiceBus', icon: 'me-2 pi pi-code' },
    { label: 'Manual | settlement actions Manual | 10/6/2023 3:40:04 PM | JoshLove-msft', icon: 'me-2 bi bi-clock-history' }, { label: 'Auto | settlement actions Auto | 10/6/2023 3:40:04 PM | JoshLove-msft', icon: 'me-2 bi bi-file-diff' }
  ];
    this.breadCrumbHome = { icon: 'pi pi-home', routerLink: '/' };
  }

  showRevisionSidePanel() {
    this.revisionsSidePanel.emit(true);
  }
}
