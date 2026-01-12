import { Component, EventEmitter, Input, Output, SimpleChanges, ViewChild, ElementRef, AfterViewInit, OnDestroy } from '@angular/core';
import { ConfigService } from 'src/app/_services/config/config.service';
import * as monaco from 'monaco-editor/esm/vs/editor/editor.api.js';
import { Subscription } from 'rxjs';

@Component({
    selector: 'app-code-editor',
    templateUrl: './code-editor.component.html',
    styleUrls: ['./code-editor.component.scss'],
    standalone: false
})
export class CodeEditorComponent implements AfterViewInit, OnDestroy {
  @Input() language: string = 'javascript';
  @Input() content: string | undefined = undefined;
  @Output() contentValueChange = new EventEmitter<string>();

  @ViewChild('editorContainer') editorContainer!: ElementRef;

  private editor: monaco.editor.IStandaloneCodeEditor | undefined;
  private themeSubscription: Subscription | undefined;

  constructor(private configService: ConfigService) {}

  ngAfterViewInit() {
    this.initEditor();
  }

  private initEditor() {
    if (this.editorContainer) {
      this.editor = monaco.editor.create(this.editorContainer.nativeElement, {
        value: this.content,
        language: this.language,
        scrollBeyondLastLine: false,
        automaticLayout: true,
        theme: 'vs-light'
      });

      this.editor.onDidChangeModelContent(() => {
        const val = this.editor?.getValue();
        if (val !== undefined) {
             this.contentValueChange.emit(val);
        }
      });

      this.themeSubscription = this.configService.appTheme$.subscribe((value: string) => {
        this.updateTheme(value);
      });
    }
  }

  updateTheme(value: string) {
      let theme = 'vs-light';
      switch (value) {
        case 'dark-theme':
        case 'dark-solarized-theme':
          theme = 'vs-dark';
          break;
        default:
          theme = 'vs-light';
      }
      monaco.editor.setTheme(theme);
  }

  ngOnChanges(changes: SimpleChanges) {
    if (!this.editor) return;

    if (changes['language'] && changes['language'].currentValue) {
      const model = this.editor.getModel();
      if (model) {
        monaco.editor.setModelLanguage(model, this.language);
      }
    }

    if (changes['content']) {
        const value = changes['content'].currentValue;
        if (value !== this.editor.getValue()) {
            this.editor.setValue(value || '');
        }
    }
  }

  ngOnDestroy() {
    if (this.editor) {
      this.editor.dispose();
    }
    if (this.themeSubscription) {
        this.themeSubscription.unsubscribe();
    }
  }
}
