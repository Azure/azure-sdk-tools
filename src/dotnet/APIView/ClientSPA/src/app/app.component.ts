import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  host: {
    class:'light-theme'
  } 
})
export class AppComponent {
  title = 'APIView';
}
