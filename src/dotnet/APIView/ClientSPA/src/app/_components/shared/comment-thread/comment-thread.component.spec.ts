import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommentThreadComponent } from './comment-thread.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { CodePanelRowData } from 'src/app/_models/codePanelRowData';
import { ReviewPageModule } from 'src/app/_modules/review-page/review-page.module';

describe('CommentThreadComponent', () => {
  let component: CommentThreadComponent;
  let fixture: ComponentFixture<CommentThreadComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CommentThreadComponent],
      imports: [
        HttpClientTestingModule,
        ReviewPageModule,
        SharedAppModule
      ],
    });
    fixture = TestBed.createComponent(CommentThreadComponent);
    component = fixture.componentInstance;
    component.codePanelRowData = new CodePanelRowData();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
