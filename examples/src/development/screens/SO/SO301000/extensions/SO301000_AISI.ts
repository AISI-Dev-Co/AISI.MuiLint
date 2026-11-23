import { PXFieldState, handleEvent, CustomEventType, ValueChangedHandlerArgs } from "client-controls";
import { SO301000, SOOrderHeader } from "src/screens/SO/SO301000/SO301000";

export interface SO301000_AISI extends SO301000 {}
export class SO301000_AISI {
    @handleEvent(CustomEventType.ValueChanged, { view: "Document", field: "UsrPriority" })
    onPriorityChanged(args: ValueChangedHandlerArgs) {
    }
}

export interface SOOrderHeader_AISI extends SOOrderHeader {}
export class SOOrderHeader_AISI {
    UsrPriority: PXFieldState;
    UsrDeliveryWindow: PXFieldState;
    UsrDeliveryNote: PXFieldState;
}
