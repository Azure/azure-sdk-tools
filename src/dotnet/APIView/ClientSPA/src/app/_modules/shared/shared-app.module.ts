import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { FooterComponent } from 'src/app/_components/shared/footer/footer.component';

@NgModule({
  declarations: [
    NavBarComponent,
    FooterComponent,
  
  ],
  exports: [
    NavBarComponent,
    FooterComponent
  ],
  imports: [
    CommonModule
  ]
})
export class SharedAppModule { }
