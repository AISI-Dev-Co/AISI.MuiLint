// Every TypeScript mistake MuiLint knows about. A .ts can extend a screen without any HTML.
import { PXFieldState, createCollection, handleEvent, CustomEventType } from "client-controls";
import { SO301000, SOLine } from "src/screens/SO/SO301000/SO301000";

export interface SO301000_BrokenTs extends SO301000 {}
export class SO301000_BrokenTs {
    // AISI0016: SOLine_BrokenTs extends SOLine, it isn't a view class
    ExtraLines = createCollection(SOLine_BrokenTs);

    // AISI0015: the view is called Transactions
    @handleEvent(CustomEventType.RowSelected, { view: "Transaction" })
    onLineSelected() {
    }
}

export interface SOLine_BrokenTs extends SOLine {}
export class SOLine_BrokenTs {
    UsrNote: PXFieldState;
}

// AISI0014: no interface says what this extends
export class SOOrderHeader_BrokenTs {
    UsrRush: PXFieldState;
}
