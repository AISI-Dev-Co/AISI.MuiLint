import { PXScreen, PXView, PXFieldState, createSingle, graphInfo, handleEvent, CustomEventType, ValueChangedHandlerArgs } from "client-controls";

@graphInfo({ graphType: "AISI.Shipping.ShipmentInquiry", primaryView: "Filter" })
export class XX301000 extends PXScreen {
    Filter = createSingle(ShipmentFilter);

    @handleEvent(CustomEventType.ValueChanged, { view: "Filter", field: "StartDat" })
    onStartDateChanged(args: ValueChangedHandlerArgs) {
    }
}

export class ShipmentFilter extends PXView {
    StartDate: PXFieldState;
}
