import { PXScreen, PXView, PXFieldState, createSingle, createCollection, graphInfo, handleEvent, CustomEventType, ValueChangedHandlerArgs, CellCssHandlerArgs, CallbackCompletedHandlerArgs } from "client-controls";

const lineEvents = { view: "Lines" };

@graphInfo({ graphType: "AISI.Shipping.ShipmentInquiry", primaryView: "Filter" })
export class XX301000 extends PXScreen {
    Filter = createSingle(ShipmentFilter);
    Lines = createCollection(ShipmentLine);

    @handleEvent(CustomEventType.ValueChanged, {
        view: "Filter",
        field: "StartDate",
    })
    onStartDateChanged(args: ValueChangedHandlerArgs) {
    }

    @handleEvent(CustomEventType.GetCellCss, { view: "Lines", allColumns: true })
    getLineCss(args: CellCssHandlerArgs) {
        return undefined;
    }

    @handleEvent(CustomEventType.CallbackCompleted)
    onCallback(args: CallbackCompletedHandlerArgs<any>) {
    }

    @handleEvent(CustomEventType.RowSelected, lineEvents)
    onLineSelected() {
    }
}

export class ShipmentFilter extends PXView {
    StartDate: PXFieldState;
}

export class ShipmentLine extends PXView {
    Qty: PXFieldState;
}
