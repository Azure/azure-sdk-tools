import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-theme-test',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './theme-test.component.html',
  styleUrls: ['./theme-test.component.scss']
})
export class ThemeTestComponent {
  // This is a development-only component for testing theme colors side-by-side
}
