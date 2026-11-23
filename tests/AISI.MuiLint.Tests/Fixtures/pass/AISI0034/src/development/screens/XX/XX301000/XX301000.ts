import { PXScreen, PXView, PXFieldState, createSingle, graphInfo, handleEvent, CustomEventType, CurrentRowChangedHandlerArgs, ValueChangedHandlerArgs } from "client-controls";

@graphInfo({ graphType: "AISI.Shipping.ShipmentInquiry", primaryView: "Filter" })
export class XX301000 extends PXScreen {
    Filter = createSingle(ShipmentFilter);

    // RowSelected and CurrentRowChanged hand over the same arguments.
    @handleEvent(CustomEventType.RowSelected, { view: "Filter" })
    onFilterSelected(args: CurrentRowChangedHandlerArgs<ShipmentFilter>) {
    }

    @handleEvent(CustomEventType.ValueChanged, { view: "Filter", field: "StartDate" })
    onStartDateChanged(args: ValueChangedHandlerArgs<ShipmentFilter>) {
    }

    @handleEvent(CustomEventType.CurrentRowChanged, { view: "Filter" })
    onFilterChanged(args: any) {
    }
}

export class ShipmentFilter extends PXView {
    StartDate: PXFieldState;
}
