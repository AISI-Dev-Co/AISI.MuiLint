import { PXScreen, PXView, PXFieldState, PXActionState, createSingle, createCollection, graphInfo } from "client-controls";

@graphInfo({ graphType: "PX.Objects.SO.SOOrderEntry", primaryView: "Document" })
export class SO301000 extends PXScreen {
    Document = createSingle(SOOrderHeader);
    Transactions = createCollection(SOLine);
}

export class SOOrderHeader extends PXView {
    OrderNbr: PXFieldState;
}

export class SOLine extends PXView {
    InventoryID: PXFieldState;
}
