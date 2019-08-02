﻿using System;
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
using System.Threading;
using System.Net.Http;
using log4net;
using log4net.Config;

namespace JsonToCsvTool
{
    class Program
    {
        private static readonly ILog logger = LogManager.GetLogger("JsonToCsvTool");        
        static readonly string ZipFilePath = ConfigurationManager.AppSettings["zipFilePath"];
        //readonly static string importPath = ConfigurationManager.AppSettings["jsonFilePath"];
        static readonly string ExportPath = ConfigurationManager.AppSettings["csvFilePath"];
        static int limitRowCount = int.Parse(ConfigurationManager.AppSettings["limitRowCount"]);
        static readonly string InsertUrl = ConfigurationManager.AppSettings["create"];
        static readonly string MutiInsertUrl = ConfigurationManager.AppSettings["mutiCreate"];
        static readonly string DeleteUrl = ConfigurationManager.AppSettings["delete"];
        static readonly int MultipleRowNumber = Convert.ToInt32(ConfigurationManager.AppSettings["MultipleRowNumber"]);
        static string _dirName = "";

        static void Main(string[] args)
        {
            //Console.WriteLine($"Start to convert json file from {ZipFilePath}");
            logger.Info($"Start to convert json file from {ZipFilePath}");

            try
            {
                StringWriter csvString = new StringWriter();

                using (var csv = new CsvWriter(csvString))
                {
                    //csv.Configuration.SkipEmptyRecords = true;
                    //csv.Configuration.WillThrowOnMissingField = false;                
                    csv.Configuration.Delimiter = ",";

                    foreach (var property in typeof(VerdictModel).GetProperties())
                    {
                        csv.WriteField(property.Name);
                    }

                    foreach (string file in Directory.EnumerateFiles(ZipFilePath, "*.zip"))
                    {
                        using (ZipFile zip = ZipFile.Read(file))
                        {
                            var verdictList = new List<VerdictModel>();
                            bool isExportCSV = false;
                            int rowCount = 0, insertedRowCount = 0;

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
                                        var model = JsonConvert.DeserializeObject<VerdictModel>(jsonString);

                                        if (verdictList.Count() == MultipleRowNumber)
                                        {
                                            var resultCount = InsertMongoDb(verdictList);

                                            if (resultCount > 0)
                                            {
                                                insertedRowCount += resultCount;
                                                Console.WriteLine($"Insert {insertedRowCount} verdict(s) already.");
                                            }
                                            else
                                            {
                                                Thread.Sleep(new TimeSpan(0, 0, 3));

                                                foreach (var verdict in verdictList)
                                                {
                                                    insertedRowCount += InsertMongoDb(verdict);
                                                    Console.WriteLine($"Insert {insertedRowCount} verdict(s) already.");
                                                }
                                            }                                            
                                            verdictList = new List<VerdictModel>();
                                        }
                                        else
                                        {
                                            if (model.date.HasValue && model.date.Value >= DateTime.Now.Date.AddYears(-1))
                                            {
                                                verdictList.Add(model);
                                            }
                                        }

                                        if (rowCount <= limitRowCount)
                                        {
                                            if (model.date.HasValue && model.date.Value.Date >= DateTime.Now.Date.AddYears(-1))
                                            {
                                                csv.NextRecord();
                                                csv.WriteField(model.date.Value.ToString("yyyy/MM/dd"));
                                                csv.WriteField(model.sys);
                                                csv.WriteField(model.reason?.Replace(Environment.NewLine, " ").Trim());
                                                csv.WriteField(model.judgement?.Replace(Environment.NewLine, " ").Trim());
                                                csv.WriteField(model.type?.Replace(Environment.NewLine, " ").Trim());
                                                csv.WriteField(model.mainText?.Replace(Environment.NewLine, " ").Trim());
                                                csv.WriteField(model.opinion?.Replace(Environment.NewLine, " ").Trim());

                                                StringBuilder _relatedIssues = new StringBuilder();
                                                foreach (var ri in model.relatedIssues)
                                                {
                                                    if (!string.IsNullOrEmpty(ri.lawName)) _relatedIssues.Append(ri.lawName.Trim());
                                                    if (!string.IsNullOrEmpty(ri.issueRef)) _relatedIssues.Append(ri.issueRef.Trim());
                                                }

                                                if (_relatedIssues.Length > 0)
                                                    csv.WriteField(_relatedIssues.ToString());

                                                rowCount++;
                                            }
                                        }
                                        else if (isExportCSV == false)
                                        {
                                            isExportCSV = true;
                                            Console.WriteLine($"{_dirName} export {rowCount} verdicts in csv.");
                                            File.WriteAllText($"{ExportPath}\\{_dirName}.csv", csvString.ToString(), Encoding.UTF8);
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

        #region old code
        //public static void InsertMongoDb(string jsonData)
        //{
        //    int tryTimes = 2;

        //    try
        //    {
        //        if (!string.IsNullOrEmpty(jsonData))
        //        {
        //            var httpWebRequest = (HttpWebRequest)WebRequest.Create(InsertUrl);
        //            httpWebRequest.ContentType = "application/json";
        //            httpWebRequest.Method = "POST";

        //            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
        //            {
        //                streamWriter.Write(jsonData);

        //                while (tryTimes > 0)
        //                {
        //                    try
        //                    {
        //                        var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

        //                        if (httpResponse.GetResponseStream() != null)
        //                        {
        //                            //using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
        //                            //{
        //                            //    var result = streamReader.ReadToEnd();
        //                            //    //Console.WriteLine($"Insert succeed: {result}");
        //                            //}
        //                            tryTimes = 0;
        //                        }
        //                        Console.WriteLine($"Post {InsertUrl} fail! Remain {tryTimes} times to try.");
        //                        tryTimes--;
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        logger.Debug($"{ex.Message}, {jsonData}");
        //                        Console.WriteLine(ex.Message);
        //                        Console.WriteLine($"Insert fail! Remain {tryTimes} times to try.");
        //                        tryTimes--;
        //                        Thread.Sleep(1000);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex);
        //    }
        //}        
        #endregion

        public static int InsertMongoDb(List<VerdictModel> verdictList)
        {
            int rowCount = 0;

            if (verdictList.Count > 0)
            {
                try
                {
                    //建立 WebRequest 並指定目標的 uri
                    WebRequest request = WebRequest.Create(MutiInsertUrl);
                    //指定 request 使用的 http verb
                    request.Method = "POST";
                    //準備 post 用資料
                    var json = JsonConvert.SerializeObject(verdictList)?.Replace(" ", "");
                    //指定 request 的 content type
                    request.ContentType = "application/json; charset=utf-8";
                    //指定 request header
                    request.Headers.Add("authorization", "token apikey");
                    //將需 post 的資料內容轉為 stream 
                    using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                    {
                        //string json = new JavaScriptSerializer().Serialize(postData);
                        streamWriter.Write(json);
                        streamWriter.Flush();
                    }

                    //使用 GetResponse 方法將 request 送出，如果不是用 using 包覆，請記得手動 close WebResponse 物件，避免連線持續被佔用而無法送出新的 request
                    using (var httpResponse = (HttpWebResponse)request.GetResponse())
                    {
                        try
                        {
                            if (httpResponse.StatusCode == HttpStatusCode.OK)
                            {
                                rowCount += verdictList.Count();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }                
            }
            return rowCount;
        }

        public static int InsertMongoDb(VerdictModel verdict)
        {
            int rowCount = 0;

            try
            {
                //建立 WebRequest 並指定目標的 uri
                WebRequest request = WebRequest.Create(InsertUrl);
                //指定 request 使用的 http verb
                request.Method = "POST";
                //準備 post 用資料
                var json = JsonConvert.SerializeObject(verdict)?.Replace(" ", "");
                //指定 request 的 content type
                request.ContentType = "application/json; charset=utf-8";
                //指定 request header
                request.Headers.Add("authorization", "token apikey");
                //將需 post 的資料內容轉為 stream 
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    //string json = new JavaScriptSerializer().Serialize(postData);
                    streamWriter.Write(json);
                    streamWriter.Flush();
                }

                //使用 GetResponse 方法將 request 送出，如果不是用 using 包覆，請記得手動 close WebResponse 物件，避免連線持續被佔用而無法送出新的 request
                using (var httpResponse = (HttpWebResponse)request.GetResponse())
                {
                    try
                    {
                        if (httpResponse.StatusCode == HttpStatusCode.OK)
                        {
                            rowCount++;
                        }
                        else
                        {
                            logger.Debug($"Insert fail: {json}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"{ex.Message}: {json}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return rowCount;
        }

        public class VerdictModel
        {
            public DateTime? date { get; set; }
            public string sys { get; set; }
            public string reason { get; set; }
            public string type { get; set; }
            public string mainText { get; set; }
            public string opinion { get; set; }
            public string judgement { get; set; }
            public class RelatedIssues
            {
                public string lawName { get; set; }
                public string issueRef { get; set; }
            }
            public List<RelatedIssues> relatedIssues { get; set; }
        }
    }
}
