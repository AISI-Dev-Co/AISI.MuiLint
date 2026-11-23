import { PXScreen, handleEvent, CustomEventType } from "client-controls";

export class XX301000 extends PXScreen {
    @handleEvent(CustomEventType.RowSelected)
    onRowSelected() {
    }
}
