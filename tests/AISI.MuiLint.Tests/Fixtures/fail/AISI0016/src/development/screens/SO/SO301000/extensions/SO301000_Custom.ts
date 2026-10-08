import { PXFieldState, createCollection } from "client-controls";
import { SO301000, SOLine } from "src/screens/SO/SO301000/SO301000";

export interface SO301000_Custom extends SO301000 {}
export class SO301000_Custom {
    // SOLine_Custom is an extension of SOLine, not a view class.
    ExtraLines = createCollection(SOLine_Custom);
}

export interface SOLine_Custom extends SOLine {}
export class SOLine_Custom {
    UsrNote: PXFieldState;
}
