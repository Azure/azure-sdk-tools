import { Pipe, PipeTransform } from '@angular/core';
import { APIRevision } from '../_models/revision';

@Pipe({
  name: 'lastUpdatedOn'
})
export class LastUpdatedOnPipe implements PipeTransform {

  transform(apiRevision: APIRevision): string {
    const lastUpdatedOn = new Date(apiRevision.lastUpdatedOn);
    const createdOn = new Date(apiRevision.createdOn);
    return (lastUpdatedOn.getTime() < createdOn.getTime()) ? apiRevision.createdOn : apiRevision.lastUpdatedOn;
  }

}
