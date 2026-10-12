import { PXScreen, createSingle, graphInfo } from "client-controls";

@graphInfo({ primaryView: "Document" })
export class XX301000 extends PXScreen {
    Document = createSingle(XXDocument);
}
