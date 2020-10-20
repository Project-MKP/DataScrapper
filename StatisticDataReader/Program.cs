using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using System.Data.SqlTypes;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Security.Cryptography.X509Certificates;

namespace StatisticDataReader
{
    /*
    * 1. Webscrapping linku do pobrania dokumentu
    * 2. Pobieranie dokumentu z odczytami i zapis na dysku
    * 3. Odczyt danych z pliku 
    * 4. Połączenie + przesyłanie danych do bazy SQL
    */
    class Program
    {
        private const string path = @"C:\Users\ch\Desktop\DataScrapper\StatisticDataReader\DataFile\"; //ścieżka do folderu w którym zapisuje się plik z danymi, w naszym przypadku jest to ścieżka na naszym serwerze.
        private const string connStr = "server=remotemysql.com;user=rsnE4IGWZE;database=rsnE4IGWZE;password=DwbWHpJ6zr;"; //ciąg zaków dla biblioteki łączącej skrypt i aplikację z bazą danych
        private static string[] locations = { "Aleje Jerozolimskie/pl. Zawiszy - suma", 
            "Banacha/Żwirki i Wigury (display)", 
            "Czerniakowska - suma", 
            "Marsa",
            "Marszałkowska/Metro Świętokrzyska", 
            "NSR Most Gdański - suma", 
            "NSR Solec - suma", 
            "Radzymińska/Jórskiego", 
            "Szaserów - suma", 
            "Targowa/Dworzec Wileński", 
            "Towarowa/Łucka", 
            "Wiertnicza", 
            "Świętokrzyska/Emilii Plater - suma",
            "Świętokrzyska/Zielna (display)",
            "Żwirki i Wigury/Trojdena - suma" };

        private static List<int> bikersCounts = new List<int>();

        static void Main(string[] args)
        {
            GetDownloadLinks();
            bikersCounts = ReadBikersData(path);
            ConnectAndFillDatabase();
        }

        static void GetDataFile(string url, string path) //funkcja odpowiedzialna za pobranie pliku z danymi.
        {
            using (WebClient wClient = new WebClient())
            {
                Console.WriteLine("Downloading data from: "+url+" to: "+path);
                wClient.DownloadFile(url, path);
            }
        }

        public static string ReplaceCharacters(string toReplace) //usuwanie niepotrzebnych dopisków i zamiana polskich znaków na angielskie
        {
            toReplace = toReplace.Replace(" - suma", "")
                    .Replace("(display)", "")
                    .Replace("/", " i ")
                    .Replace('Ś', 'S')
                    .Replace('Ż', 'Z')
                    .Replace('ł', 'l')
                    .Replace('ę', 'e')
                    .Replace('ń', 'n')
                    .Replace('Ż', 'Z')
                    .Replace('ó', 'o')
                    .Replace('Ł', 'L')
                    .Replace('Ż', 'Z');
            return toReplace;
        }

        static List<int> ReadBikersData(string path) //odczyt i zapais do zmiennych danych oraz daty z pobranego pliku.
        {
            List<int> bikers = new List<int>();
            string[] dataArray = File.ReadLines(path + "data.txt")
                .Last()
                .Split(',');

            for(int i = 0; i <= 16; i++)
            {
                if(i > 1)
                {
                    bikers.Add(int.Parse(dataArray[i]));
                }
            }
            return bikers;
        }

        static void ConnectAndFillDatabase() //połączenie z bazą danych sql + aktulizacja nowych danych
        {
            Console.WriteLine("Connecting to database");
            using var con = new MySqlConnection(connStr);
            con.Open();
            string sql1 = "SELECT * FROM data";
            using var cmd1 = new MySqlCommand(sql1, con);
            using MySqlDataReader rdr = cmd1.ExecuteReader();
            Console.WriteLine("Filling data");
            if (!rdr.HasRows)
            {
                con.Close();

                con.Open();

                string sql = "INSERT INTO data (address, quantity) VALUES (@address, @quantity)";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@address", MySqlDbType.VarChar);
                cmd.Parameters.Add("@quantity", MySqlDbType.VarChar);

                for (int i = 0; i <= 14; i++)
                {
                    cmd.Parameters["@address"].Value = ReplaceCharacters(locations[i]);
                    cmd.Parameters["@quantity"].Value = bikersCounts[i];
                    cmd.ExecuteNonQuery();
                }
            }
            else 
            {
                con.Close();
                con.Open();
                
                string sql = "UPDATE data SET address = @address, quantity = @quantity WHERE id = @id" ;
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@address", MySqlDbType.VarChar);
                cmd.Parameters.Add("@quantity", MySqlDbType.VarChar);
                cmd.Parameters.Add("@id", MySqlDbType.Int32);

                for (int i = 1; i <= 15; i++)
                {
                    cmd.Parameters["@id"].Value = i;
                    cmd.Parameters["@address"].Value = ReplaceCharacters(locations[i - 1]);
                    cmd.Parameters["@quantity"].Value = bikersCounts[i-1];
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("Finished!");
        }

        static void GetDownloadLinks() //funkcja używająca selenium w celu pobrania danych.
        {
            const string bikeDatabase = "http://greenelephant.pl/shiny/rowery/";

            Console.WriteLine("Starting browser");
            FirefoxOptions options = new FirefoxOptions();
            options.AddArguments("--headless");

            using (FirefoxDriver fDriver = new FirefoxDriver(options)) //do webscrappingu korzystamy z przeglądarki firefox
            {
                fDriver.Navigate().GoToUrl(bikeDatabase);
                
                while (true)
                {
                    try
                    {
                        fDriver.FindElement(By.XPath("//span[contains(text(),'Marsa')]"));
                        break;
                    }
                    catch { }
                    Thread.Sleep(1000);
                }

                foreach (string x in locations)
                {
                    Console.WriteLine("Checking data for: "+x);
                    fDriver.FindElement(By.XPath("//span[contains(text(),'" + x + "')]")).Click();
                }

                string downloadLink = fDriver.FindElement(By.XPath("//a[@id='eksport']")).GetAttribute("href");
                GetDataFile(downloadLink, path+"data.txt");
                fDriver.Close();
            }
        }
    }
}
