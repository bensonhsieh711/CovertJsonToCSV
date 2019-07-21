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

namespace JsonToCsvTool
{
    class Program
    {
        readonly static string zipFilePath = ConfigurationSettings.AppSettings["zipFilePath"];
        readonly static string importPath = ConfigurationSettings.AppSettings["jsonFilePath"];
        readonly static string exportPath = ConfigurationSettings.AppSettings["csvFilePath"];
        static int limitRowCount = int.Parse(ConfigurationSettings.AppSettings["limitRowCount"]);
        static string dirName = "";

        static void Main(string[] args)
        {
            Console.WriteLine($"Start to convert json file from {zipFilePath}");

            try
            {
                StringWriter csvString = new StringWriter();

                try
                {
                    using (var csv = new CsvWriter(csvString))
                    {
                        //csv.Configuration.SkipEmptyRecords = true;
                        //csv.Configuration.WillThrowOnMissingField = false;                
                        csv.Configuration.Delimiter = ",";

                        foreach (var property in typeof(Model).GetProperties())
                        {
                            csv.WriteField(property.Name);
                        }

                        foreach (string file in Directory.EnumerateFiles(zipFilePath, "*.zip"))
                        {
                            using (ZipFile zip = ZipFile.Read(file))
                            {
                                int rowCount = 0;                                

                                foreach (ZipEntry zipEntry in zip)
                                {
                                    if (zipEntry.IsDirectory)
                                    {
                                        dirName = zipEntry.FileName.TrimEnd('/');
                                    }
                                    else if (zipEntry.IsText)
                                    {
                                        using (var ms = new MemoryStream())
                                        {
                                            zipEntry.Extract(ms);
                                            ms.Position = 0;
                                            var sr = new StreamReader(ms, Encoding.UTF8);
                                            var jsonString = sr.ReadToEnd();
                                            var model = JsonConvert.DeserializeObject<Model>(jsonString);

                                            if (model.date.HasValue && model.date.Value.Date >= DateTime.Now.AddYears(-1))
                                            {
                                                csv.NextRecord();
                                                csv.WriteField(model.mainText?.Replace(Environment.NewLine, " ").Trim());
                                                csv.WriteField(model.opinion?.Replace(Environment.NewLine, " ").Trim());
                                                csv.WriteField(model.judgement?.Replace(Environment.NewLine, " ").Trim());
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
                                Console.WriteLine($"Total export {rowCount} data raws.");
                                File.WriteAllText($"{exportPath}\\{dirName}.csv", csvString.ToString(), Encoding.UTF8);                                
                            }
                            //File.Delete($"{importPath}\\{dirName}");
                        }
                    }
                    Console.WriteLine($"Export csv file done");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Covert to csv fail: {ex.ToString()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }

            Console.WriteLine($"Convert to csv file done.");
        }

        public class Model
        {
            public DateTime? date { get; set; }
            public string mainText { get; set; }
            public string opinion { get; set; }
            public string judgement { get; set; }
        }
    }
}
