import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'approval'
})
export class ApprovalPipe implements PipeTransform {

  transform(isApproved: boolean | undefined): string {
    if (isApproved === undefined) {
      return "";
    }
    return isApproved ? "Approved" : "Pending";
  }

}
