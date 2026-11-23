import { PXFieldState, handleEvent, CustomEventType, RowSelectedHandlerArgs } from "client-controls";
import { XX301000, ShipmentFilter } from "src/screens/XX/XX301000/XX301000";

export interface XX301000_Custom extends XX301000 {}
export class XX301000_Custom {
    // Two handlers may share a name: client-controls keeps both.
    @handleEvent(CustomEventType.RowSelected, { view: "Filter" })
    onFilterSelected(args: RowSelectedHandlerArgs) {
        this.refreshCarrierTotals();
    }

    refreshCarrierTotals() {
    }
}

export interface ShipmentFilter_Custom extends ShipmentFilter {}
export class ShipmentFilter_Custom {
    UsrCarrier: PXFieldState;
}
