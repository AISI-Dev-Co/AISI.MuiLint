import { PXFieldState } from "client-controls";
import { SO301000, SOOrderHeader } from "../SO301000";

export interface SO301000_AISI extends SO301000 {}
export class SO301000_AISI {}

export interface SOOrderHeader_AISI extends SOOrderHeader {}
export class SOOrderHeader_AISI {
	UsrPriority: PXFieldState;
	UsrDeliveryWindow: PXFieldState;
	UsrDeliveryNote: PXFieldState;
}
