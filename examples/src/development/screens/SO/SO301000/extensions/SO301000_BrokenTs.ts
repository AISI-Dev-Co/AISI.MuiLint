// Every TypeScript mistake MuiLint knows about. A .ts can extend a screen without any HTML.
import { PXView, PXFieldState, createCollection, handleEvent, gridConfig, CustomEventType } from "client-controls";
import { SO301000, SOLine } from "src/screens/SO/SO301000/SO301000";

export interface SO301000_BrokenTs extends SO301000 {}
export class SO301000_BrokenTs {
    // AISI0016: SOLine_BrokenTs extends SOLine, it isn't a view class
    ExtraLines = createCollection(SOLine_BrokenTs);

    Notes = createCollection(SONote_BrokenTs);

    // AISI0015: the view is called Transactions
    @handleEvent(CustomEventType.RowSelected, { view: "Transaction" })
    onLineSelected() {
    }
}

export interface SOLine_BrokenTs extends SOLine {}
export class SOLine_BrokenTs {
    UsrNote: PXFieldState;
}

// AISI0026: a grid's view class gets a preset (Primary, Inquiry, Details...)
@gridConfig({ syncPosition: true })
export class SONote_BrokenTs extends PXView {
    NoteText: PXFieldState;
}

// AISI0014: no interface says what this extends
export class SOOrderHeader_BrokenTs {
    UsrRush: PXFieldState;
}
