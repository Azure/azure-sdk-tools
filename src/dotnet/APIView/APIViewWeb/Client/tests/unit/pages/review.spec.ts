import { test, expect } from '@playwright/test';
import * as rvM from "../../../src/pages/review.module";

test.describe('CodeLine Section State Management', () => {
    test('updateCodeLineSectionState should update cookie value', async ({ page }) => {
        expect(rvM.updateCodeLineSectionState("", "24", rvM.CodeLineSectionState.hidden)).toBe("");
        expect(rvM.updateCodeLineSectionState("", "24", rvM.CodeLineSectionState.shown)).toBe("24");
        expect(rvM.updateCodeLineSectionState("24", "20", rvM.CodeLineSectionState.hidden)).toBe("24");
        expect(rvM.updateCodeLineSectionState("24", "20", rvM.CodeLineSectionState.shown)).toBe("24,20");
        expect(rvM.updateCodeLineSectionState("24,20", "5", rvM.CodeLineSectionState.shown)).toBe("24,20,5");
        expect(rvM.updateCodeLineSectionState("24,20,5", "19", rvM.CodeLineSectionState.hidden)).toBe("24,20,5");
        expect(rvM.updateCodeLineSectionState("24,20,5", "12", rvM.CodeLineSectionState.shown)).toBe("24,20,5,12");
        expect(rvM.updateCodeLineSectionState("24,20,5", "12", rvM.CodeLineSectionState.shown)).toBe("24,20,5,12");
        expect(rvM.updateCodeLineSectionState("24,20,5,12", "20", rvM.CodeLineSectionState.hidden)).toBe("24,5,12");
        expect(rvM.updateCodeLineSectionState("24,5,12", "24", rvM.CodeLineSectionState.hidden)).toBe("5,12");
        expect(rvM.updateCodeLineSectionState("5,12", "12", rvM.CodeLineSectionState.hidden)).toBe("5");
        expect(rvM.updateCodeLineSectionState("5", "5", rvM.CodeLineSectionState.hidden)).toBe("");
    });
});