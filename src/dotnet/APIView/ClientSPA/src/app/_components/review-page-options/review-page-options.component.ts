import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { InputSwitchOnChangeEvent } from 'primeng/inputswitch';

@Component({
  selector: 'app-review-page-options',
  templateUrl: './review-page-options.component.html',
  styleUrls: ['./review-page-options.component.scss']
})
export class ReviewPageOptionsComponent implements OnInit, OnChanges{
  @Input() isDiffView: boolean = false;

  @Input() onlyDiffInput: boolean | undefined;
  @Output() onlyDiffEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();

  comments : boolean = true;
  systemComments: boolean = true;
  documentation: boolean = true;
  lineNumber: boolean = true;
  leftNavigation: boolean = true;
  onlyDiff : boolean | undefined;

  ngOnInit() {
    this.onlyDiff = this.onlyDiffInput ?? false;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['onlyDiffInput']) {
      this.onlyDiff = this.onlyDiffInput ?? this.onlyDiff;
    }
  }

  /**
 * Callback to invoke on row selection.
 * @param event the Filter event
 */
  onOnlyDiffChange(event: InputSwitchOnChangeEvent) {
    this.onlyDiffEmitter.emit(event.checked);
  }
}
