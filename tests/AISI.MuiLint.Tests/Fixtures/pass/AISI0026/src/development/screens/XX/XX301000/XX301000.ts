import { PXView, PXFieldState, GridPreset, gridConfig } from "client-controls";

@gridConfig({ preset: GridPreset.Details, syncPosition: true })
export class XXLine extends PXView {
    LineNbr: PXFieldState;
}
