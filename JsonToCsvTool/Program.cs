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

namespace JsonToCsvTool
{
    class Program
    {
        readonly static string importPath = ConfigurationSettings.AppSettings["jsonFilePath"];
        readonly static string exportPath = ConfigurationSettings.AppSettings["csvFilePath"];
        static int rowCount = int.Parse(ConfigurationSettings.AppSettings["rowCount"]);

        static void Main(string[] args)
        {
            Console.WriteLine($"Start to convert json file from {importPath}");

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

                        foreach (string file in Directory.EnumerateFiles(importPath, "*.json"))
                        {
                            if (rowCount > 0)
                            {
                                Console.WriteLine($"Processing {Path.GetFileName(file)}");
                                string contents = File.ReadAllText(file);
                                var model = JsonConvert.DeserializeObject<Model>(contents);
                                csv.NextRecord();
                                csv.WriteField(model.mainText?.Replace(Environment.NewLine, " ").Trim());
                                csv.WriteField(model.opinion?.Replace(Environment.NewLine, " ").Trim());
                                csv.WriteField(model.judgement?.Replace(Environment.NewLine, " ").Trim());
                                rowCount--;
                            }
                            else
                            {
                                break;
                            }
                        }

                        string dirName = new DirectoryInfo(importPath).Name;
                        File.WriteAllText($"{exportPath}\\{dirName}.csv", csvString.ToString(), Encoding.UTF8);
                        Console.WriteLine($"Export csv file done");
                    }
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
            public string mainText { get; set; }
            public string opinion { get; set; }
            public string judgement { get; set; }
        }
    }
}
