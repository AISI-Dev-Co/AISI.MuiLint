import { PXView, PXFieldState, createCollection } from "client-controls";
import { SO301000 } from "src/screens/SO/SO301000/SO301000";

export interface SO301000_Custom extends SO301000 {}
export class SO301000_Custom {
    ExtraLines = createCollection(ExtraLine);
}

export class ExtraLine extends PXView {
    UsrNote: PXFieldState;
}
