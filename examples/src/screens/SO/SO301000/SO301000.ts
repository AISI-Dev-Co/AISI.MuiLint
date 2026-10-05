// A made-up, trimmed stand-in for the stock Sales Orders screen, to match SO301000.html.
import { PXScreen, PXView, PXFieldState, PXActionState, createSingle, createCollection, graphInfo } from "client-controls";

@graphInfo({ graphType: "PX.Objects.SO.SOOrderEntry", primaryView: "Document" })
export class SO301000 extends PXScreen {
    AddInvBySite: PXActionState;
    Document = createSingle(SOOrderHeader);
    Transactions = createCollection(SOLine);
}

export class SOOrderHeader extends PXView {
    OrderType: PXFieldState;
    OrderNbr: PXFieldState;
    Status: PXFieldState;
    OrderDate: PXFieldState;
    CustomerID: PXFieldState;
    LocationID: PXFieldState;
}

export class SOLine extends PXView {
    InventoryID: PXFieldState;
    OrderQty: PXFieldState;
}
