export class ChangeHistory {
    changeAction: string = '';
    changedBy: string = '';
    changedOn: string | null = null;
    notes: string = '';

    constructor() {
        this.changeAction = '';
        this.changedBy = '';
        this.changedOn = null;
        this.notes = '';
    }
}