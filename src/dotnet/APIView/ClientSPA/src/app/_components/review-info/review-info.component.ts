import { Component, Input, OnInit, SimpleChanges } from '@angular/core';
import { DropdownChangeEvent } from 'primeng/dropdown';
import { Review } from 'src/app/_models/review';
import { Revision } from 'src/app/_models/revision';

@Component({
  selector: 'app-review-info',
  templateUrl: './review-info.component.html',
  styleUrls: ['./review-info.component.scss']
})
export class ReviewInfoComponent {
  @Input() review : Review | undefined = undefined;
  @Input() reviewRevisions : Map<string, Revision[]> = new Map<string, Revision[]>();

  revisionsTypeDropDown: any[] = [];
  selectedRevisionsType: any | undefined;

  revisionsDropDown: any[] = [];
  selectedRevisionsDropDown: any | undefined;

  diffRevisionsTypeDropDown: any[] = [];
  selectedDiffRevisionsType: any | undefined;

  diffRevisionsDropDown: any[] = [];
  selecteddiffRevisionsDropDown: any | undefined;

  badgeClass : Map<string, string> = new Map<string, string>();

  constructor() {
    this.revisionsTypeDropDown = this.getReviewRevisionType();
    this.diffRevisionsTypeDropDown = this.getReviewRevisionType();
    this.selectedRevisionsType = this.revisionsTypeDropDown[0];
    this.selectedDiffRevisionsType = this.diffRevisionsTypeDropDown[0];

    // Set Badge Class for Icons
    this.badgeClass.set("Pending", "fa-solid fa-circle-minus text-warning");
    this.badgeClass.set("Approved", "fas fa-check-circle text-success");
    this.badgeClass.set("Manual", "fa-solid fa-arrow-up-from-bracket");
    this.badgeClass.set("PullRequest", "fa-solid fa-code-pull-request");
    this.badgeClass.set("Automatic", "fa-solid fa-robot");
  }

  ngOnChanges(changes: SimpleChanges) {
    console.log(this.selectedRevisionsType);
    if (changes["reviewRevisions"].currentValue && changes["reviewRevisions"].currentValue.size > 0 ) {
      this.revisionsDropDown = this.getReviewRevisionsDropDown(this.selectedRevisionsType);
      this.diffRevisionsDropDown = this.getReviewRevisionsDropDown(this.selectedDiffRevisionsType);
    }
  }

  getReviewRevisionType() {
    return [
      { name: 'Manual', value: 'Manual' },
      { name: 'Automatic', value: 'Automatic' },
      { name: 'Pull Request', value: 'PullRequest' }
    ];
  }

  onRevisionTypeChange(event: DropdownChangeEvent) {
    this.revisionsDropDown = this.getReviewRevisionsDropDown(this.selectedRevisionsType);
  }

  onDiffRevisionTypeChange(event: DropdownChangeEvent) {
    this.diffRevisionsDropDown = this.getReviewRevisionsDropDown(this.selectedDiffRevisionsType);
  }

  /**
   * Retrieve revision of a specified type.
   * @param selectedType the selected type for revision or diff revision
   */
  getReviewRevisionsDropDown(selectedType : any) {
    const revisions : any[] = [];
    this.reviewRevisions.get(selectedType.value)?.forEach((revision: { label: any; id: any; }) => {
      revisions.push({ name: revision.label, value: revision.id });
    });
    return revisions;
  }
}
