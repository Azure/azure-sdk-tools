import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MessageService } from 'primeng/api';

import { NamespacePageComponent } from './namespace-page.component';

describe('NamespacePageComponent', () => {
  let component: NamespacePageComponent;
  let fixture: ComponentFixture<NamespacePageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        NamespacePageComponent,
        RouterTestingModule,
        HttpClientTestingModule
      ],
      providers: [
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NamespacePageComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('getStatusClass', () => {
    it('should return correct class for Approved status', () => {
      expect(component.getStatusClass('Approved' as any)).toBe('status-approved');
    });

    it('should return correct class for Proposed status', () => {
      expect(component.getStatusClass('Proposed' as any)).toBe('status-proposed');
    });
  });

  describe('getStatusLabel', () => {
    it('should return "Approved" for Approved status', () => {
      expect(component.getStatusLabel('Approved' as any)).toBe('Approved');
    });

    it('should return "Proposed" for Proposed status', () => {
      expect(component.getStatusLabel('Proposed' as any)).toBe('Proposed');
    });

    it('should return "Rejected" for Rejected status', () => {
      expect(component.getStatusLabel('Rejected' as any)).toBe('Rejected');
    });
  });
});
