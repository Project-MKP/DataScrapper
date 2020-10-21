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
using System.Dynamic;

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
        private const string path = @"C:\Users\Krystian\Desktop\DataScrapper\StatisticDataReader\DataFile\"; //ścieżka do folderu w którym zapisuje się plik z danymi, w naszym przypadku jest to ścieżka na naszym serwerze.
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

        private static int freeBikes;

        private static List<string> weatherData = new List<string>();
        private static List<int> bikersCounts = new List<int>();
        private static List<int> veturiloData = new List<int>();

        static void Main(string[] args)
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    GetDownloadLinks();
                    bikersCounts = ReadBikersData(path);
                    veturiloData = GetBikesData();
                    weatherData = GetWeather();
                    ConnectAndFillDatabase();
                    FillVeturiloStationsData();
                    FillWheatherData();
                    Console.WriteLine("gówno");
                    Thread.Sleep(86400000); //co 24h 86400000ms
                }
            });

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    freeBikes = GetFreeBikes();
                    FillFreeBikesData(freeBikes);
                    Console.WriteLine("cycki");
                    Thread.Sleep(5000); //co 5s 5000ms
                }
            });
            Console.ReadKey();
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
                    .Replace(" (display)", "")
                    .Replace("Banacha/", "Banacha oraz ")
                    .Replace("/Trojdena", " oraz Trojdena")
                    .Replace("pl.", "plac")
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
                rdr.Close();

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
                rdr.Close();
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
            con.Close();
            Console.WriteLine("Finished!");
        }

        static void FillVeturiloStationsData()
        {
            using var con = new MySqlConnection(connStr);
            con.Open();
            string sql1 = "SELECT * FROM veturilo";
            using var cmd1 = new MySqlCommand(sql1, con);
            using MySqlDataReader rdr = cmd1.ExecuteReader();
            if (!rdr.HasRows)
            {
                rdr.Close();
                string sql = "INSERT INTO veturilo (stations, bikes) VALUES (@stations, @bikes)";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@stations", MySqlDbType.VarChar);
                cmd.Parameters.Add("@bikes", MySqlDbType.VarChar);


                cmd.Parameters["@stations"].Value = veturiloData[0];
                cmd.Parameters["@bikes"].Value = veturiloData[1];
                cmd.ExecuteNonQuery();
            }
            else
            {
                rdr.Close();
                string sql = "UPDATE veturilo SET stations = @stations, bikes = @bikes WHERE id = @id";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@stations", MySqlDbType.VarChar);
                cmd.Parameters.Add("@bikes", MySqlDbType.VarChar);
                cmd.Parameters.Add("@id", MySqlDbType.Int32);

                cmd.Parameters["@id"].Value = 1;
                cmd.Parameters["@stations"].Value = veturiloData[0];
                cmd.Parameters["@bikes"].Value = veturiloData[1];
                cmd.ExecuteNonQuery();
            }
            con.Close();
        }

        static void FillFreeBikesData(int bikes)
        {
            using var con = new MySqlConnection(connStr);
            con.Open();
            string sql1 = "SELECT * FROM veturilo";
            using var cmd1 = new MySqlCommand(sql1, con);
            using MySqlDataReader rdr = cmd1.ExecuteReader();
            if (!rdr.HasRows)
            {
                rdr.Close();
                string sql = "INSERT INTO veturilo (freeb) VALUES (@freeb)";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@freeb", MySqlDbType.VarChar);
             
                cmd.Parameters["@freeb"].Value = bikes;
                cmd.ExecuteNonQuery();
            }
            else
            {
                rdr.Close();
                string sql = "UPDATE veturilo SET freeb = @freeb WHERE id = @id";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@freeb", MySqlDbType.VarChar);
                cmd.Parameters.Add("@id", MySqlDbType.Int32);

                cmd.Parameters["@id"].Value = 1;
                cmd.Parameters["@freeb"].Value = bikes;
                cmd.ExecuteNonQuery();
            }
            con.Close();
        }

        static void FillWheatherData()
        {
            using var con = new MySqlConnection(connStr);
            con.Open();
            string sql1 = "SELECT * FROM wheather";
            using var cmd1 = new MySqlCommand(sql1, con);
            using MySqlDataReader rdr = cmd1.ExecuteReader();
            if (!rdr.HasRows)
            {
                rdr.Close();
                string sql = "INSERT INTO wheather (temp, wheather, airq, imgurl) VALUES (@temp, @wheather, @airq, @imgurl)";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@temp", MySqlDbType.VarChar);
                cmd.Parameters.Add("@wheather", MySqlDbType.VarChar);
                cmd.Parameters.Add("@airq", MySqlDbType.VarChar);
                cmd.Parameters.Add("@imgurl", MySqlDbType.VarChar);

                cmd.Parameters["@temp"].Value = weatherData[0];
                cmd.Parameters["@wheather"].Value = weatherData[1];
                cmd.Parameters["@airq"].Value = weatherData[2];
                cmd.Parameters["@imgurl"].Value = weatherData[3];
                cmd.ExecuteNonQuery();
            }
            else 
            {
                rdr.Close();
                string sql = "UPDATE wheather SET temp = @temp, wheather = @wheather, airq = @airq, imgurl = @imgurl  WHERE id = @id";
                using var cmd = new MySqlCommand(sql, con);

                cmd.Parameters.Add("@temp", MySqlDbType.VarChar);
                cmd.Parameters.Add("@wheather", MySqlDbType.VarChar);
                cmd.Parameters.Add("@airq", MySqlDbType.VarChar);
                cmd.Parameters.Add("@imgurl", MySqlDbType.VarChar);
                cmd.Parameters.Add("@id", MySqlDbType.Int32);

                cmd.Parameters["@id"].Value = 1;
                cmd.Parameters["@temp"].Value = weatherData[0];
                cmd.Parameters["@wheather"].Value = weatherData[1];
                cmd.Parameters["@airq"].Value = weatherData[2];
                cmd.Parameters["@imgurl"].Value = weatherData[3];
                cmd.ExecuteNonQuery();
            }
            con.Close ();
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

        static List<int> GetBikesData() //webscrapping danych o rowerach veturilo
        {
            const string url = "https://www.veturilo.waw.pl/mapa-stacji/";
            int allBikes = 0;

            List<int> veturiloData = new List<int>(); //po kolei dane: ilość stacji, ilość rowerów

            FirefoxOptions options = new FirefoxOptions();
            options.AddArguments("--headless");

            using (FirefoxDriver fDriver = new FirefoxDriver(options))
            {
                fDriver.Navigate().GoToUrl(url);

                var link = fDriver.FindElements(By.TagName("tr"));
                veturiloData.Add(link.Count() - 1); //odejmujemy nagłówek
                Console.WriteLine(link.Count() - 1);

                for (int i = 2; i <= link.Count() - 1; i++)
                {
                    allBikes += int.Parse(fDriver.FindElement(By.XPath("//tr[" + i + "]/td[3]")).Text);
                }
                veturiloData.Add(allBikes);

                fDriver.Close();
            }
            return veturiloData;
        }

        static int GetFreeBikes() //aktualnie wole rowery
        {
            const string url = "https://www.veturilo.waw.pl/mapa-stacji/";
            int freeBikes = 0;

            FirefoxOptions options = new FirefoxOptions();
            options.AddArguments("--headless");

            using (FirefoxDriver fDriver = new FirefoxDriver(options))
            {
                fDriver.Navigate().GoToUrl(url);

                var link = fDriver.FindElements(By.TagName("tr"));
                veturiloData.Add(link.Count() - 1); //odejmujemy nagłówek
                Console.WriteLine(link.Count() - 1);

                for (int i = 2; i <= link.Count() - 1; i++)
                {
                    freeBikes += int.Parse(fDriver.FindElement(By.XPath("//tr[" + i + "]/td[2]")).Text);
                }
                veturiloData.Add(freeBikes);

                fDriver.Close();
            }

            return freeBikes;
        }

        static List<string> GetWeather() //webscrapping pogody
        {
            const string url = "https://pogoda.interia.pl/prognoza-szczegolowa-warszawa,cId,36917";

            List<string> weatherData = new List<string>(); //po kolei dane: temperatura, opis, jakość powietrzal, link do obrazka

            FirefoxOptions options = new FirefoxOptions();
            options.AddArguments("--headless");

            using (FirefoxDriver fDriver = new FirefoxDriver(options))
            {
                fDriver.Navigate().GoToUrl(url);

                weatherData.Add(fDriver.FindElement(By.XPath("//div[@class='weather-currently-temp-strict']")).Text);
                weatherData.Add(fDriver.FindElement(By.XPath("//li[@class='weather-currently-icon-description']")).Text);
                weatherData.Add(fDriver.FindElement(By.XPath("//div[@class='kind']/div[@class='value']")).Text);

                fDriver.Navigate().GoToUrl("https://pogoda.onet.pl/prognoza-pogody/warszawa-357732");
                weatherData.Add(fDriver.FindElement(By.XPath("//div[@class='mainParams']//div[@class='forecast']//span[@class='iconHolder']//img[@class='svg']")).GetAttribute("src"));
                
                fDriver.Close();
            }
            return weatherData;
        }
    }
}
