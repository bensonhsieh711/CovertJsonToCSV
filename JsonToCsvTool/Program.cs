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

        static void Main(string[] args)
        {
            Console.WriteLine($"Start to convert json file from {importPath}");

            try
            {
                foreach (string file in Directory.EnumerateFiles(importPath, "*.json"))
                {
                    Console.WriteLine($"Processing {Path.GetFileName(file)}");
                    string contents = File.ReadAllText(file);                    
                    var csv = jsonToCSV(contents, ",");
                    File.WriteAllText($"{exportPath}\\{Path.GetFileNameWithoutExtension(file)}.csv", csv, Encoding.UTF8);
                    Console.WriteLine($"Export csv file {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }

            Console.WriteLine($"Convert to csv file done.");
        }

        public static string jsonToCSV(string jsonContent, string delimiter)
        {
            StringWriter csvString = new StringWriter();

            try
            {
                using (var csv = new CsvWriter(csvString))
                {
                    //csv.Configuration.SkipEmptyRecords = true;
                    //csv.Configuration.WillThrowOnMissingField = false;                
                    csv.Configuration.Delimiter = delimiter;
                    var model = jsonStringList(jsonContent);

                    foreach (var property in model.GetType().GetProperties())
                    {
                        csv.WriteField(property.Name);
                    }
                    csv.NextRecord();
                    csv.WriteField(model.mainText?.Replace(Environment.NewLine, " "));
                    csv.WriteField(model.opinion?.Replace(Environment.NewLine, " "));
                    csv.WriteField(model.judgement?.Replace(Environment.NewLine, " "));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Covert to csv fail: {ex.ToString()}");
            }
            return csvString.ToString();
        }

        public static Model jsonStringList(string jsonContent)
        {
            var result = JsonConvert.DeserializeObject<Model>(jsonContent);
            return result;
        }

        public class Model
        {
            public string mainText { get; set; }
            public string opinion { get; set; }
            public string judgement { get; set; }
        }
    }
}
