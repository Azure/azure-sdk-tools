import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { RevisionsListComponent } from './revisions-list.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { AppModule } from 'src/app/app.module';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('RevisionListComponent', () => {
  let component: RevisionsListComponent;
  let fixture: ComponentFixture<RevisionsListComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
    declarations: [RevisionsListComponent],
    imports: [SharedAppModule,
        AppModule],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
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
