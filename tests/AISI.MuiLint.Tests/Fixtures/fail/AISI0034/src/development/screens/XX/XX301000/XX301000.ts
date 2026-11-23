import { PXScreen, PXView, PXFieldState, createSingle, graphInfo, handleEvent, CustomEventType, ValueChangedHandlerArgs } from "client-controls";

@graphInfo({ graphType: "AISI.Shipping.ShipmentInquiry", primaryView: "Filter" })
export class XX301000 extends PXScreen {
    Filter = createSingle(ShipmentFilter);

    @handleEvent(CustomEventType.RowSelected, { view: "Filter" })
    onFilterSelected(args: ValueChangedHandlerArgs) {
    }
}

export class ShipmentFilter extends PXView {
    StartDate: PXFieldState;
}
