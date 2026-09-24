import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MenubarModule } from 'primeng/menubar';
import { RevisionOptionsComponent } from 'src/app/_components/revision-options/revision-options.component';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { UserProfile } from 'src/app/_models/userProfile';
import { environment } from 'src/environments/environment';

@Component({
    selector: 'app-review-info',
    templateUrl: './review-info.component.html',
    styleUrls: ['./review-info.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        MenubarModule,
        RevisionOptionsComponent,
        LanguageNamesPipe
    ]
})
export class ReviewInfoComponent {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() userProfile: UserProfile | undefined;

  @Input() review : Review | undefined = undefined;

  assetsPath : string = environment.assetsPath;
}
