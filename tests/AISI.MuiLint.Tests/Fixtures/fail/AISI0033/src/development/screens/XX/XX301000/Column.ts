import { PXScreen, PXView, PXFieldState, createCollection, handleEvent, CustomEventType, CellCssHandlerArgs } from "client-controls";

export class XX301000 extends PXScreen {
    Lines = createCollection(ShipmentLine);

    @handleEvent(CustomEventType.GetCellCss, { view: "Lines", column: "Quantity" })
    getQtyCss(args: CellCssHandlerArgs) {
        return undefined;
    }
}

export class ShipmentLine extends PXView {
    Qty: PXFieldState;
}
