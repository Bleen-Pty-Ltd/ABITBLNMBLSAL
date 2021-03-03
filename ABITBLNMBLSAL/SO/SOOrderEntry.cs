using System;
using System.Collections;
using System.Collections.Generic;
using PX.Data;
using PX.Objects.CS;
using PX.Objects.IN;

namespace PX.Objects.SO
{
    public class SOOrderEntry_Extension : PXGraphExtension<SOOrderEntry>
    {
        public PXAction<SOOrder> IssueStock;

        [PXButton()]
        [PXUIField(DisplayName = "Issue Stock")]
        public virtual IEnumerable issueStock(PXAdapter adapter)
        {
            foreach (SOOrder order in adapter.Get<SOOrder>())
            {
                SOShipmentEntry shipGraph = PXGraph.CreateInstance<SOShipmentEntry>();
                var created = new DocumentList<SOShipment>(shipGraph);
                shipGraph.CreateShipment(Base.Document.Current, GetPreferedSiteID(), Base.Accessinfo.BusinessDate, false, SOOperation.Issue, created, PXQuickProcess.ActionFlow.NoFlow);

                if (created.Count > 0)
                {
                    shipGraph.Clear();
                    SOShipment shipment = shipGraph.Document.Search<SOShipment.shipmentNbr>(created[0].ShipmentNbr);
                    PressConfirmShipment(shipment);
                    using (new PXTimeStampScope(null))
                    {
                        shipGraph.Clear();
                        shipGraph.Document.Current = shipGraph.Document.Search<SOShipment.shipmentNbr>(shipment.ShipmentNbr);

                        Dictionary<string, string> parameters = new Dictionary<string, string>();
                        parameters["SOShipment.ShipmentNbr"] = shipment.ShipmentNbr;

                        throw new PXReportRequiredException(parameters, "SO644000");
                    }
                }
            }
            return adapter.Get();

        }

        protected void _(Events.RowSelected<SOOrder> e)
        {
            SOOrder row = e.Row;
            if (row == null) return;
            IssueStock.SetVisible(row.OrderType == "IA" || row.OrderType == "IO" || row.OrderType == "IV");
        }

        private Int32? GetPreferedSiteID()
        {
            int? siteID = null;
            PXResultset<SOOrderSite> osites = PXSelectJoin<SOOrderSite,
                InnerJoin<INSite, On<SOOrderSite.FK.Site>>,
                Where<SOOrderSite.orderType, Equal<Current<SOOrder.orderType>>,
                    And<SOOrderSite.orderNbr, Equal<Current<SOOrder.orderNbr>>,
                        And<Match<INSite, Current<AccessInfo.userName>>>>>>.Select(Base);
            SOOrderSite preferred;
            if (osites.Count == 1)
            {
                siteID = ((SOOrderSite)osites).SiteID;
            }
            else if ((preferred = PXSelectJoin<SOOrderSite,
                        InnerJoin<INSite,
                            On<SOOrderSite.FK.Site>>,
                        Where<SOOrderSite.orderType, Equal<Current<SOOrder.orderType>>,
                            And<SOOrderSite.orderNbr, Equal<Current<SOOrder.orderNbr>>,
                                And<SOOrderSite.siteID, Equal<Current<SOOrder.defaultSiteID>>,
                                    And<Match<INSite, Current<AccessInfo.userName>>>>>>>.Select(Base)) != null)
            {
                siteID = preferred.SiteID;
            }
            return siteID;
        }


        private void PressConfirmShipment(SOShipment shipment)
        {
            SOShipmentEntry docgraph = PXGraph.CreateInstance<SOShipmentEntry>();
            docgraph.Document.Current = docgraph.Document.Search<SOShipment.shipmentNbr>(shipment.ShipmentNbr);
            foreach (var action in (docgraph.action.GetState(null) as PXButtonState).Menus)
            {
                if (action.Command == "ConfirmShipmentAction")
                {
                    PXAdapter adapter2 = new PXAdapter(new DummyView(docgraph, docgraph.Document.View.BqlSelect, new List<object> { docgraph.Document.Current }));
                    adapter2.Menu = action.Command;
                    docgraph.action.PressButton(adapter2);

                    TimeSpan timespan;
                    Exception ex;
                    while (PXLongOperation.GetStatus(docgraph.UID, out timespan, out ex) == PXLongRunStatus.InProcess)
                    { }
                    break;
                }
            }
        }

        internal class DummyView : PXView
        {
            List<object> _Records;
            internal DummyView(PXGraph graph, BqlCommand command, List<object> records)
                : base(graph, true, command)
            {
                _Records = records;
            }
            public override List<object> Select(object[] currents, object[] parameters, object[] searches, string[] sortcolumns, bool[] descendings, PXFilterRow[] filters, ref int startRow, int maximumRows, ref int totalRows)
            {
                return _Records;
            }
        }
    }
}