import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../../test-setup';
import { FormsModule } from '@angular/forms';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { DialogModule } from 'primeng/dialog';
import { vi } from 'vitest';
import { AICommentDeleteDialogComponent } from './ai-comment-delete-dialog.component';

describe('AICommentDeleteDialogComponent', () => {
  let component: AICommentDeleteDialogComponent;
  let fixture: ComponentFixture<AICommentDeleteDialogComponent>;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        AICommentDeleteDialogComponent,
        FormsModule,
        DialogModule,
        NoopAnimationsModule
      ]
    });
    fixture = TestBed.createComponent(AICommentDeleteDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should not allow deletion without a reason', () => {
    component.reason = '';
    expect(component.canDelete).toBeFalsy();
  });

  it('should not allow deletion with only whitespace', () => {
    component.reason = '   ';
    expect(component.canDelete).toBeFalsy();
  });

  it('should not emit deleteConfirm on delete without reason', () => {
    vi.spyOn(component.deleteConfirm, 'emit');
    component.commentId = 'test-123';
    component.reason = '';

    component.onDelete();

    expect(component.deleteConfirm.emit).not.toHaveBeenCalled();
  });

  it('should reset form after successful delete', () => {
    component.reason = 'This comment is wrong';

    component.onDelete();

    expect(component.reason).toBe('');
  });

  it('should reset form on cancel', () => {
    component.reason = 'Some reason';
    component.visible = true;

    vi.spyOn(component.visibleChange, 'emit');
    vi.spyOn(component.cancel, 'emit');

    component.onCancel();

    expect(component.reason).toBe('');
    expect(component.visible).toBe(false);
    expect(component.visibleChange.emit).toHaveBeenCalledWith(false);
    expect(component.cancel.emit).toHaveBeenCalled();
  });

  it('should reset form on hide', () => {
    component.reason = 'Some reason';

    vi.spyOn(component.cancel, 'emit');

    component.onHide();

    expect(component.reason).toBe('');
    expect(component.cancel.emit).toHaveBeenCalled();
  });
});
