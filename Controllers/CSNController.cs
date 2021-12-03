using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Xml.Linq;

namespace LaborationCSN.Controllers
{
    public class CSNController : Controller // Här skrivs all kod
    {
        SQLiteConnection sqlite;

        //Upprätthåller anslutning till databasen
        public CSNController()
        {
            string path = HostingEnvironment.MapPath("/db/");
            sqlite = new SQLiteConnection($@"DataSource={path}\csn.sqlite");

        }

        // Stoppa in queryn 
        XElement SQLResult(string query, string root, string nodeName)
        {
            sqlite.Open();

            var adapt = new SQLiteDataAdapter(query, sqlite);
            var ds = new DataSet(root);
            adapt.Fill(ds, nodeName);
            XElement xe = XElement.Parse(ds.GetXml());

            sqlite.Close();
            return xe;
        }


        //
        // GET: /Csn/Test
        // 
        // Testmetod som visar på hur ni kan arbeta från SQL till XML till
        // presentations-xml som sedan används i vyn.
        // Lite överkomplicerat för just detta enkla fall men visar på idén.
        public ActionResult Test()
        {
            string query = @"SELECT a.Arendenummer, s.Beskrivning, SUM(((Sluttid-starttid +1) * b.Belopp)) as Summa
                            FROM Arende a, Belopp b, BeviljadTid bt, BeviljadTid_Belopp btb, Stodform s, Beloppstyp blt
                            WHERE a.Arendenummer = bt.Arendenummer AND s.Stodformskod = a.Stodformskod
                            AND btb.BeloppID = b.BeloppID AND btb.BeviljadTidID = bt.BeviljadTidID AND b.Beloppstypkod = blt.Beloppstypkod AND b.BeloppID LIKE '%2009'
							Group by a.Arendenummer
							Order by a.Arendenummer ASC";
            XElement test = SQLResult(query, "BeviljadeTider", "BeviljadTid");
            XElement summa = new XElement("Total",
                (from b in test.Descendants("Summa")
                 select (int)b).Sum());
            test.Add(summa);

            // skicka presentations xml:n till vyn /Views/Csn/Test,
            // i vyn kommer vi åt den genom variabeln "Model"
            return View(test);
        }

        //
        // GET: /Csn/Index

        public ActionResult Index()
        {
            return View();
        }


        //
        // GET: /Csn/Uppgift1
        // Tar in en XML fil och gör en query på denna fil returnerar sedan reultatet till view

        public ActionResult Uppgift1()
        {
            string query = @"SELECT a.Arendenummer, UtbetDatum, utb.UtbetStatus, SUM(((Sluttid-starttid +1) * b.Belopp)) as Summa, s.Beskrivning
                                FROM Arende a, Belopp b, Utbetalning utb, Utbetalningsplan up, UtbetaldTid ut, UtbetaldTid_Belopp ut_b, Stodform s
                                WHERE a.Arendenummer = up.Arendenummer AND up.UtbetPlanID = utb.UtbetPlanID AND utb.UtbetID = ut.UtbetID 
                                AND ut.UtbetTidID = ut_b.UtbetaldTidID AND ut_b.BeloppID = b.BeloppID AND a.Stodformskod = s.Stodformskod
                                Group by a.Arendenummer, UtbetDatum";
            XElement uppg1 = SQLResult(query, "Utbetalningar", "utbet");

            XElement test = new XElement("Utbetalning", from a in uppg1.Descendants("Arendenummer")
                                                group a by a.Value into b
                                                select new XElement("Arende", new XElement("Arendenummer", b.Key),
                                                (from utbet in uppg1.Descendants("utbet")
                                                 where utbet.Element("Arendenummer").Value == b.Key
                                                 select utbet.Element("Beskrivning")).FirstOrDefault(),
                                                from utbet in uppg1.Descendants("utbet")
                                                where utbet.Element("Arendenummer").Value == b.Key
                                                select new XElement("Utbetalning", utbet.Element("UtbetDatum"), utbet.Element("UtbetStatus"), utbet.Element("Summa")),
                                                new XElement("Total", (from utbet in uppg1.Descendants("utbet")
                                                                       where utbet.Element("Arendenummer").Value == b.Key
                                                                       select (int)utbet.Element("Summa")).Sum()),
                                                new XElement("Utbetald", (from utbet in uppg1.Descendants("utbet")
                                                                          where utbet.Element("UtbetStatus").Value == "Utbetald" && utbet.Element("Arendenummer").Value == b.Key
                                                                          select (int)utbet.Element("Summa")).Sum()),
                                                new XElement("Kvarvarande", (from utbet in uppg1.Descendants("utbet")
                                                                             where utbet.Element("Arendenummer").Value == b.Key && utbet.Element("UtbetStatus").Value == "Planerad"
                                                                             select (int)utbet.Element("Summa")).Sum())));

            return View(test);

        }


        //
        // GET: /Csn/Uppgift2   
        // Tar in en XML fil och gör en query på denna fil returnerar sedan reultatet till view
        public ActionResult Uppgift2()
        {
            string query = @"SELECT utb.UtbetDatum, bt.Beskrivning, SUM(((Sluttid-starttid +1) * b.Belopp)) as Summa
                                from Belopp b, Utbetalning utb, Utbetalningsplan up, UtbetaldTid ut, UtbetaldTid_Belopp ut_b, Beloppstyp bt
                                WHERE up.UtbetPlanID = utb.UtbetPlanID AND utb.UtbetID = ut.UtbetID 
                                AND ut.UtbetTidID = ut_b.UtbetaldTidID AND ut_b.BeloppID = b.BeloppID AND b.Beloppstypkod = bt.Beloppstypkod
                                Group by bt.Beskrivning, utb.UtbetDatum
                                Order by UtbetDatum";

            XElement uppg2 = SQLResult(query, "Utbetalningar", "Utbet");

            XElement test = new XElement("Utb", from a in uppg2.Descendants("UtbetDatum")
                                                group a by a.Value into d
                                                select new XElement("Datum", new XElement("UtbetDatum", d.Key),
                                                from b in uppg2.Descendants("Utbet")
                                                where b.Element("UtbetDatum").Value == d.Key
                                                select new XElement("Utbetalning", b.Element("Beskrivning"), b.Element("Summa")),
                                                new XElement("Total", (from b in uppg2.Descendants("Utbet")
                                                                       where b.Element("UtbetDatum").Value == d.Key
                                                                       select (int)b.Element("Summa")).Sum())));
            return View(test);
        }

        //  GET: /Csn/Uppgift3


        public ActionResult Uppgift3()
        {
            string query = @"SELECT bt.Starttid, bt.Sluttid, form.Beskrivning,SUM(((Sluttid-starttid +1) * b.Belopp)) AS Summa
                                FROM BeviljadTid bt, Stodform form, Belopp b, Arende a, BeviljadTid_Belopp btb
                                WHERE bt.BeviljadTidID=btb.BeviljadTidID AND b.BeloppID=btb.BeloppID AND a.Stodformskod=form.Stodformskod AND a.Arendenummer=bt.Arendenummer
                                GROUP BY bt.BeviljadTidID;";
            XElement uppg3 = SQLResult(query, "BeviljadeTider2009", "BeviljadTid");

            // skicka presentations xml:n till vyn /Views/Csn/Test,
            // i vyn kommer vi åt den genom variabeln "Model"
            return View(uppg3);
        }
    }
}