using cdn_api;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static WysylkaPDFConsole.Pdf;

namespace WysylkaPDFConsole
{
    class Program
    {

        private static int XLWersjaAPI = 20232;
        private static int XLTrybWsadowy = 1;
        private static int XLWinieta = -1;
        private static int XLUtworzWlasnaSesje = 1;
        private static string XLSQLServer = "192.168.4.24";
        private static string XLSQLDatabase = "Termedia";//"Termedia TEST";//"ERPXL_PoloMarket"
        private static string XLUser = "ADMIN"; //
        private static string XLPass = "NE!w0rk12";//"Dupa2003!";
        private static string connectionString = @"Data Source=192.168.4.24;Initial Catalog=ERPXL_Termedia; User ID=onesupport;Password=a956!2A@";
        static void Main(string[] args)
        {

            TextWriter tw2 = new StreamWriter(@"C:\OneSupport\WysylkaPDF\log.txt", append: true);
            tw2.WriteLine();
            tw2.WriteLine();
            tw2.WriteLine();
            tw2.WriteLine();
            tw2.WriteLine("Hello " + DateTime.Now.ToString("f"));
            //tw2.WriteLine();
            tw2.WriteLine();
            int sesja = 0;

            string pdfyQuery = "SELECT [OWFs_ID] ,[OWFs_GidTyp] ,[OWFs_GidNumer]  ,[OWFs_GidFirma] ,[OWFs_Numer] ,[OWFs_Miesiac] ,[OWFs_Rok] FROM [CDN].[ONE_WydrukiFS] where [OWFs_Wyslano] IS NULL or [OWFs_Wyslano]=0";
            List<ListaPdf> ListPdfy = SelectListPdfy(pdfyQuery, "cdn.ONE_WydrukiFS", connectionString, tw2);

            logowanie(ref sesja, tw2);

            foreach (ListaPdf fs in ListPdfy)
            {
                tw2.WriteLine("Wysyłanie pdf " + fs.numerDok);
                string miesiac = fs.miesiac;
                if (fs.miesiac.Length == 1)
                    miesiac = "0" + fs.miesiac;

                XLWydrukInfo_20232 wydruk = new XLWydrukInfo_20232();
                wydruk.Wersja = XLWersjaAPI;
                wydruk.Zrodlo = 1;
                wydruk.Wydruk = 2;
                wydruk.Format = 3;
                wydruk.FiltrSQL = "(TrN_GIDTyp=" + fs.gidTyp + " AND TrN_GIDNumer=" + fs.gidNumer + ")";
                wydruk.Urzadzenie = 3;
                wydruk.DrukujDoPliku = 1;
                wydruk.PlikDocelowy = "C:\\wydruki\\" + fs.numerDok.Replace("\\", "_").Replace("/", "_") + ".pdf";
                //MessageBox.Show(wydruk.PlikDocelowy);
                int blad_wydruk = cdn_api.cdn_api.XLWykonajPodanyWydruk(wydruk);

                //MessageBox.Show(wydruk.PlikDocelowy);

                if (blad_wydruk != 0)
                {
                    tw2.WriteLine("Błąd generowanie wydruku API: " + blad_wydruk);
                }

                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    FtpWebRequest request =
                        (FtpWebRequest)WebRequest.Create("ftp://46.28.10.147/" + fs.rok + "/" + miesiac + "/" + fs.numerDok.Replace("\\", "_").Replace("/", "_") + ".pdf");
                    request.Credentials = new NetworkCredential("invoices", "6L%fn!rtx^Z3xMRj");
                    request.EnableSsl = true;
                    request.Method = WebRequestMethods.Ftp.UploadFile;

                    ServicePointManager.ServerCertificateValidationCallback =
                    (s, certificate, chain, sslPolicyErrors) => true;

                    using (Stream fileStream = File.OpenRead(wydruk.PlikDocelowy))
                    using (Stream ftpStream = request.GetRequestStream())
                    {
                        fileStream.CopyTo(ftpStream);
                    }
                    /* using (var client = new WebClient())
                     {
                         client.Credentials = new NetworkCredential("invoices", "6L%fn!rtx^Z3xMRj");
                        // client.Request.EnableSsl = false;
                         client.UploadFile("ftp://46.28.10.147/" + rok + "/" + miesiac + "/"+ numerFaktury.Replace("\\", "_").Replace("/", "_") + ".pdf", WebRequestMethods.Ftp.UploadFile, wydruk.PlikDocelowy);
                     }*/



                    tw2.WriteLine("Wysłano wydruk przez FTP dla faktury: " + fs.numerDok);
                    //string delete = "delete from cdn.ONE_WydrukiFS where OWFs_ID=" + fs.ID;
                    //DeleteFrom(delete, connectionString);
                    string update = "update cdn.ONE_WydrukiFS set [OWFs_Wyslano]=1 where OWFs_ID=" + fs.ID;
                    Update(update, tw2);
                }
                catch (WebException we)
                {
                    tw2.WriteLine("Błąd wysłania wydruku przez FTP: " + we.Message);

                    XLAtrybutInfo_20232 atr = new XLAtrybutInfo_20232();
                    atr.GIDFirma = (int)fs.gidFirma;
                    atr.GIDLp = 0;
                    atr.GIDNumer = (int)fs.gidNumer;
                    atr.GIDTyp = (int)fs.gidTyp;
                    atr.GIDSubLp = 0;
                    atr.Klasa = "Wysyłka FTP";
                    atr.Wartosc = "Błąd";
                    atr.Wersja = XLWersjaAPI;

                    int bladAtr = cdn_api.cdn_api.XLDodajAtrybut(sesja, atr);
                    if (bladAtr != 0)
                        tw2.WriteLine("Błąd podczas dodawania atrybutu Wysyłka FTP API: " + bladAtr);

                }
                File.Delete(wydruk.PlikDocelowy);


            }
            wylogowanie(sesja);

            tw2.Close();

        }


        public static string Update(string sql, TextWriter tw2)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter();
                    connection.Open();
                    adapter.UpdateCommand = new SqlCommand(sql, connection);
                    adapter.UpdateCommand.ExecuteNonQuery();
                    //MessageBox.Show("Row updated !! ");
                }
            }
            catch (Exception ex)
            {
                tw2.WriteLine(ex.ToString());
            }
            return sql;
        }
        public static List<ListaPdf> SelectListPdfy(string query, string srcTableName, string connection_string, TextWriter tw2)
        {
            List<ListaPdf> str = new List<ListaPdf>();
            string sttr = "";
            try
            {
                using (SqlConnection connection = new SqlConnection(connection_string))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter();
                    adapter.SelectCommand = new SqlCommand(query, connection);
                    DataSet dataSet = new DataSet();
                    adapter.Fill(dataSet, srcTableName);
                    foreach (DataRow row in (InternalDataCollectionBase)dataSet.Tables[0].Rows)
                    {

                        str.Add(new ListaPdf()
                        {
                            ID = Convert.ToInt32(row[0].ToString()),
                            gidTyp = Convert.ToInt32(row[1].ToString()),
                            gidNumer = Convert.ToInt32(row[2].ToString()),
                            gidFirma = Convert.ToInt32(row[3].ToString()),
                            numerDok = row[4].ToString(),
                            miesiac = row[5].ToString(),
                            rok = row[6].ToString(),
                        });



                    }
                }

            }
            catch (Exception ex)
            {
                tw2.WriteLine(sttr = sttr + "Bład SQL:" + ex.ToString() + "\n");
            }
            return str;
        }

        public static string DeleteFrom(string sql, string connection_string)       //Użycie delete i inserta nie jest optymalne, ale w niektórych przypadkach pozycje nie miały atrybutu. Po testach zmienić na update
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connection_string))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter();
                    connection.Open();
                    adapter.DeleteCommand = new SqlCommand(sql, connection);
                    adapter.DeleteCommand.ExecuteNonQuery();
                    //MessageBox.Show("Row deleted!! ");
                }
            }
            catch (Exception ex)
            {
               // tw2(ex.ToString(), "Komunikat", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return sql;
        }

        static public void logowanie(ref int sesja, TextWriter tw2)
        {
            XLLoginInfo_20232 nowyUzytkownik = new XLLoginInfo_20232();
            nowyUzytkownik.Wersja = XLWersjaAPI;
            nowyUzytkownik.Winieta = XLWinieta;
            nowyUzytkownik.TrybWsadowy = XLTrybWsadowy;
            nowyUzytkownik.UtworzWlasnaSesje = XLUtworzWlasnaSesje;
            nowyUzytkownik.SerwerKlucza = XLSQLServer;
            nowyUzytkownik.Baza = XLSQLDatabase;
            nowyUzytkownik.OpeIdent = XLUser;
            nowyUzytkownik.OpeHaslo = XLPass;
            nowyUzytkownik.PortHTTPSerweraKlucza = 1;
            nowyUzytkownik.RodzajSerweraKlucza = 0;
            nowyUzytkownik.SerwerKlucza = "localhost::5000185870";
            nowyUzytkownik.ProgramID = "ONE-044 Wysyłka PDF";

            int blad = cdn_api.cdn_api.XLLogin(nowyUzytkownik, ref sesja);

            if (blad != 0)
            {
                tw2.WriteLine("Błąd logowania: " + blad);
            }

        }

        static public void wylogowanie(int sesja)
        {
            cdn_api.cdn_api.XLLogout(sesja);
        }

    }
}
