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

namespace StatisticDataReader
{
    /*
    * 1. Webscrapping linku do pobierania
    * 2. Pobieranie danych ze strony do txt
    * 3. Czytanie danych z pliku txt
    * 4. Połączenie + przesyłanie danych do bazy SQL
    */

    class Program
    {
        private static string dataDate;
        private const string path = @"C:\Users\Krystian\Desktop\DataScrapper\StatisticDataReader\DataFile\";
        private const string conStr = "";
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
            //GetDownloadLinks();
            bikersCounts = ReadBikersData(path);
            ConnectToDatabase();
            
            Console.WriteLine("Data: "+dataDate);
            for (int i = 0; i <= 14; i++)
            {
                Console.WriteLine(locations[i]+": "+bikersCounts[i]);
            }
            Console.ReadKey();
        }

        static void GetDataFile(string url, string path) //funkcja odpowiedzialna za pobranie pliku z danymi.
        {
            using (WebClient Client = new WebClient())
            {
                Client.DownloadFile(url, path);
            }
        }
        
        static List<int> ReadBikersData(string path) //odczyt i zapais do zmiennych danych oraz daty z pobranego pliku.
        {
            List<int> bikers = new List<int>();
            string[] dataArray = File.ReadLines(path + "data.txt")
                .Last()
                .Split(',');

            for(int i = 0; i <= 16; i++)
            {
                if(i == 1)
                {
                    dataDate = dataArray[i];


                }
                else if(i > 1)
                {
                    bikers.Add(int.Parse(dataArray[i]));
                }
            }
            return bikers;
        }

        static void ConnectToDatabase()
        {

            string connStr = "server=sql7.freesqldatabase.com;user=sql7370562;database=sql7370562;password=T1HSwIdfN2;";
            using var con = new MySqlConnection(connStr);
            con.Open();
            
            string sql1 = "SELECT * FROM data";
            using var cmd1 = new MySqlCommand(sql1, con);
            using MySqlDataReader rdr = cmd1.ExecuteReader();

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
                    cmd.Parameters["@address"].Value = locations[i];
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
                    cmd.Parameters["@address"].Value = i;
                    cmd.Parameters["@address"].Value = locations[i - 1];
                    cmd.Parameters["@quantity"].Value = bikersCounts[i-1];
                    cmd.ExecuteNonQuery();
                }
            }

        }

        static void GetDownloadLinks() //funkcja używająca selenium w celu pobrania danych.
        {
            const string bikeDatabase = "http://greenelephant.pl/shiny/rowery/";

            using (FirefoxDriver fDriver = new FirefoxDriver())
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
                    Console.WriteLine(x);
                    fDriver.FindElement(By.XPath("//span[contains(text(),'" + x + "')]")).Click();
                }

                Thread.Sleep(20000);
                string downloadLink = fDriver.FindElement(By.XPath("//a[@id='eksport']")).GetAttribute("href");
                GetDataFile(downloadLink, path+"data.txt");
                fDriver.Close();
            }
        }
    }
}
