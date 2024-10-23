import { Component, EventEmitter, Input, Output, SimpleChanges } from '@angular/core';
import { ConfigService } from 'src/app/_services/config/config.service';


@Component({
  selector: 'app-code-editor',
  templateUrl: './code-editor.component.html',
  styleUrls: ['./code-editor.component.scss']
})
export class CodeEditorComponent {
  @Input() language: string | undefined = undefined;
  @Input() content: string | undefined = undefined;
  @Output() contentValueChange = new EventEmitter<string>();

  editorOptions : any = {};

  constructor(private configService: ConfigService) {}

  ngOnInit() {
    this.editorOptions.scrollBeyondLastLine = false;
    this.editorOptions.automaticLayout = true;
    this.configService.appTheme$.subscribe((value: string) => {
      switch (value) {
        case 'dark-theme':
          this.editorOptions.theme = 'vs-dark';
          break;
        case 'light-theme':
          this.editorOptions.theme = 'vs-light';
          break;
        case 'dark-solarized-theme':
          this.editorOptions.theme = 'vs-dark';
          break;
        default:
          this.editorOptions.theme = 'vs-light';
      }
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['language'] && changes['language'].currentValue) {
      this.editorOptions.language = this.language;
    }
  }

  onInit(editor: any) {
    editor.onDidChangeModelContent((event: any) => {
     this.contentValueChange.emit(editor.getValue());
    });
  }
}
