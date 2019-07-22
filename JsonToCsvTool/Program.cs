using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using CsvHelper;
using System.Data;
using Newtonsoft.Json;
using Ionic.Zip;
using System.Net;

namespace JsonToCsvTool
{
    class Program
    {
        static readonly string ZipFilePath = ConfigurationManager.AppSettings["zipFilePath"];
        //readonly static string importPath = ConfigurationManager.AppSettings["jsonFilePath"];
        static readonly string ExportPath = ConfigurationManager.AppSettings["csvFilePath"];
        //static int limitRowCount = int.Parse(ConfigurationManager.AppSettings["limitRowCount"]);
        static readonly string InsertUrl = ConfigurationManager.AppSettings["create"];
        static readonly string DeleteUrl = ConfigurationManager.AppSettings["delete"];
        static string _dirName = "";

        static void Main(string[] args)
        {
            Console.WriteLine($"Start to convert json file from {ZipFilePath}");

            try
            {
                StringWriter csvString = new StringWriter();

                using (var csv = new CsvWriter(csvString))
                {
                    //csv.Configuration.SkipEmptyRecords = true;
                    //csv.Configuration.WillThrowOnMissingField = false;                
                    csv.Configuration.Delimiter = ",";

                    foreach (var property in typeof(Model).GetProperties())
                    {
                        csv.WriteField(property.Name);
                    }

                    foreach (string file in Directory.EnumerateFiles(ZipFilePath, "*.zip"))
                    {
                        using (ZipFile zip = ZipFile.Read(file))
                        {
                            int rowCount = 0;

                            foreach (ZipEntry zipEntry in zip)
                            {
                                if (zipEntry.IsDirectory)
                                {
                                    _dirName = zipEntry.FileName.TrimEnd('/');
                                }
                                else if (zipEntry.IsText)
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        zipEntry.Extract(ms);
                                        ms.Position = 0;
                                        var sr = new StreamReader(ms, Encoding.UTF8);
                                        var jsonString = sr.ReadToEnd();
                                        InsertMongoDb(jsonString);
                                        var model = JsonConvert.DeserializeObject<Model>(jsonString);

                                        if (model.date.HasValue && model.date.Value.Date >= DateTime.Now.AddYears(-1))
                                        {
                                            csv.NextRecord();
                                            csv.WriteField(model.date.Value.ToString("yyyy/MM/dd HH:mm:ss"));
                                            csv.WriteField(model.sys);
                                            csv.WriteField(model.reason?.Replace(Environment.NewLine, " ").Trim());
                                            csv.WriteField(model.judgement?.Replace(Environment.NewLine, " ").Trim());
                                            csv.WriteField(model.type?.Replace(Environment.NewLine, " ").Trim());
                                            csv.WriteField(model.mainText?.Replace(Environment.NewLine, " ").Trim());
                                            csv.WriteField(model.opinion?.Replace(Environment.NewLine, " ").Trim());
                                            csv.WriteField(
                                                model.relatedIssues?.Replace(Environment.NewLine, " ").Trim());
                                            rowCount++;
                                        }

                                        sr.Dispose();
                                        ms.Dispose();
                                    }

                                    //zipEntry.Extract(importPath, ExtractExistingFileAction.OverwriteSilently);                                        
                                    //Console.WriteLine($"Extract {zipEntry.FileName} done.");                                        
                                    //string contents = File.ReadAllText($"{importPath}\\{zipEntry.FileName}");
                                    //var model = JsonConvert.DeserializeObject<Model>(contents);

                                    //if (model.date.HasValue && model.date.Value.Date >= DateTime.Now.AddYears(-1))
                                    //{
                                    //    csv.NextRecord();
                                    //    csv.WriteField(model.mainText?.Replace(Environment.NewLine, " ").Trim());
                                    //    csv.WriteField(model.opinion?.Replace(Environment.NewLine, " ").Trim());
                                    //    csv.WriteField(model.judgement?.Replace(Environment.NewLine, " ").Trim());
                                    //    rowCount++;
                                    //}
                                }
                            }

                            Console.WriteLine($"Total export {rowCount} data rows.");
                            File.WriteAllText($"{ExportPath}\\{_dirName}.csv", csvString.ToString(), Encoding.UTF8);
                        }

                        //File.Delete($"{importPath}\\{dirName}");
                    }
                }

                Console.WriteLine($"Export csv file done");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }

            Console.WriteLine($"Convert to csv file done.");
        }

        public static void InsertMongoDb(string jsonData)
        {
            try
            {
                if (!string.IsNullOrEmpty(jsonData))
                {
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(InsertUrl);
                    httpWebRequest.ContentType = "application/json";
                    //httpWebRequest.ContentType = "application/x-www-form-urlencoded";

                    httpWebRequest.Method = "POST";

                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        //string jsonData = "{\"user\":\"test\"," +
                        //              "\"password\":\"bla\"}";
                        streamWriter.Write(jsonData);
                    }

                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                    if (httpResponse.GetResponseStream() != null)
                    {
                        using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                        {
                            var result = streamReader.ReadToEnd();
                            Console.WriteLine($"Insert succeed: {result}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            
        }
        public class Model
        {
            public DateTime? date { get; set; }
            public string sys { get; set; }
            public string reason { get; set; }
            public string type { get; set; }
            public string relatedIssues { get; set; }
            public string mainText { get; set; }
            public string opinion { get; set; }
            public string judgement { get; set; }
        }
    }
}
