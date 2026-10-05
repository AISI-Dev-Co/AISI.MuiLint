import { PXScreen, PXView, PXFieldState, PXActionState, createSingle, createCollection, graphInfo } from "client-controls";
import { SOLine } from "./views";

@graphInfo({ graphType: "PX.Objects.SO.SOOrderEntry", primaryView: "Document" })
export class SO301000 extends PXScreen {
    AddInvBySite: PXActionState;
    Document = createSingle(SOOrderHeader);
    Transactions = createCollection(SOLine);
}

export class SOOrderHeader extends PXView {
    OrderType: PXFieldState;
    OrderNbr: PXFieldState;
    OrderDate: PXFieldState;
}
