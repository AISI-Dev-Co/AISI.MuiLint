import { PXView, PXFieldState, gridConfig } from "client-controls";

@gridConfig({ syncPosition: true })
export class XXLine extends PXView {
    LineNbr: PXFieldState;
}
