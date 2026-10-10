import { PXScreen, createSingle, graphInfo } from "client-controls";

@graphInfo({ graphType: "AISI.Demo.XXDocumentEntry", primaryView: "Document" })
export class XX301000 extends PXScreen {
    Document = createSingle(XXDocument);
}
