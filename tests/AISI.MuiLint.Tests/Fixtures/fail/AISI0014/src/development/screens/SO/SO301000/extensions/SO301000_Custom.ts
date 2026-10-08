import { PXFieldState } from "client-controls";
import { SOOrderHeader } from "src/screens/SO/SO301000/SO301000";

// The class name lost its underscore, so neither half finds the other.
export interface SOOrderHeader_Custom extends SOOrderHeader {}
export class SOOrderHeaderCustom {
    UsrPriority: PXFieldState;
}
