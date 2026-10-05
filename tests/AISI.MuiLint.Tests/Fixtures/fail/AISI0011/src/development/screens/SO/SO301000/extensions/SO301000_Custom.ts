import { PXFieldState } from "client-controls";
import { SO301000, SOOrderHeader } from "src/screens/SO/SO301000/SO301000";

export interface SO301000_Custom extends SO301000 {}
export class SO301000_Custom {}

export interface SOOrderHeader_Custom extends SOOrderHeader {}
export class SOOrderHeader_Custom {
    UsrPriority: PXFieldState;
}
