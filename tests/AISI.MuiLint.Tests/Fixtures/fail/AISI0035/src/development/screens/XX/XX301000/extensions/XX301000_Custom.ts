import { PXFieldState } from "client-controls";
import { XX301000, ShipmentFilter } from "src/screens/XX/XX301000/XX301000";

export interface XX301000_Custom extends XX301000 {}
export class XX301000_Custom {
    refreshTotals() {
    }
}

export interface ShipmentFilter_Custom extends ShipmentFilter {}
export class ShipmentFilter_Custom {
    UsrCarrier: PXFieldState;
}
