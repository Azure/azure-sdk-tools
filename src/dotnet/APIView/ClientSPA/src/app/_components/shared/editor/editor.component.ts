import { AfterViewInit, Component, Input } from '@angular/core';

@Component({
  selector: 'app-editor',
  templateUrl: './editor.component.html',
  styleUrls: ['./editor.component.scss']
})
export class EditorComponent {
  @Input() content: string = '';
  allowAnyOneToResolve : boolean = true;
}
 