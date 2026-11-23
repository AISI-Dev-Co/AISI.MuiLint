import { PXScreen, PXView, PXFieldState, createSingle, createCollection, graphInfo, handleEvent, CustomEventType, ValueChangedHandlerArgs, CellCssHandlerArgs } from "client-controls";
import { SharedLine } from "some-library";

@graphInfo({ graphType: "AISI.Shipping.ShipmentInquiry", primaryView: "Filter" })
export class XX301000 extends PXScreen {
    Filter = createSingle(ShipmentFilter);
    Lines = createCollection(SharedLine);

    @handleEvent(CustomEventType.ValueChanged, { view: "Filter", field: "StartDate" })
    onStartDateChanged(args: ValueChangedHandlerArgs) {
    }

    // SharedLine comes from a package, so its fields are unknown and nothing is reported.
    @handleEvent(CustomEventType.GetCellCss, { view: "Lines", column: "Anything" })
    getLineCss(args: CellCssHandlerArgs) {
        return undefined;
    }
}

export class ShipmentFilter extends PXView {
    StartDate: PXFieldState;
}
