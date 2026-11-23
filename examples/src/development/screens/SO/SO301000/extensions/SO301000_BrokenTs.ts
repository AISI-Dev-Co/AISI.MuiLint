// Every TypeScript mistake MuiLint knows about. A .ts can extend a screen without any HTML.
import { PXView, PXFieldState, createCollection, handleEvent, gridConfig, CustomEventType, ValueChangedHandlerArgs, RowCssHandlerArgs } from "client-controls";
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

    // AISI0032: ValueChanged is raised for one field, and this names none
    @handleEvent(CustomEventType.ValueChanged, { view: "Document" })
    onCustomerChanged(args: ValueChangedHandlerArgs) {
    }

    // AISI0033: SOLine's field is OrderQty
    // AISI0034: GetCellCss hands over CellCssHandlerArgs
    @handleEvent(CustomEventType.GetCellCss, { view: "Transactions", column: "Quantity" })
    getQuantityCss(args: RowCssHandlerArgs) {
        return undefined;
    }

    // AISI0035: SO301000 has a refreshTotals() of its own, and this one replaces it
    refreshTotals() {
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
