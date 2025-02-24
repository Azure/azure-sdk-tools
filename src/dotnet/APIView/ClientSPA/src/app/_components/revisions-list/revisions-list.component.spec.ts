import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { RevisionsListComponent } from './revisions-list.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { AppModule } from 'src/app/app.module';

describe('RevisionListComponent', () => {
  let component: RevisionsListComponent;
  let fixture: ComponentFixture<RevisionsListComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [RevisionsListComponent],
      imports: [
        HttpClientTestingModule,
        SharedAppModule,
        AppModule
      ]
    });
    fixture = TestBed.createComponent(RevisionsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should update List Details Message onChage', () => {
    component.showDeletedAPIRevisions = true;
    expect(component.apiRevisionsListDetail).toContain('Deleted APIRevision(s)');
    component.showDeletedAPIRevisions = false;
    expect(component.apiRevisionsListDetail).not.toContain('Deleted APIRevision(s)')
    component.showAPIRevisionsAssignedToMe = true;
    expect(component.apiRevisionsListDetail).toContain('Assigned to Me');
    component.showAPIRevisionsAssignedToMe = false;
    expect(component.apiRevisionsListDetail).not.toContain('Assigned to Me')
  });
});
