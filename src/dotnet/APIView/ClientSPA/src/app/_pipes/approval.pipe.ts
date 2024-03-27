import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'approval'
})
export class ApprovalPipe implements PipeTransform {

  transform(isApproved: boolean): string {
    return isApproved ? "Approved" : "Pending";
  }

}
