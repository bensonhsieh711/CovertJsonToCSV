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
using System.Threading;
using System.Net.Http;
using log4net;
using log4net.Config;
using System.Text.RegularExpressions;

namespace JsonToCsvTool
{
    class Program
    {
        private static readonly ILog logger = LogManager.GetLogger("JsonToCsvTool");
        static readonly string ZipFilePath = ConfigurationManager.AppSettings["zipFilePath"];
        //readonly static string importPath = ConfigurationManager.AppSettings["jsonFilePath"];
        static readonly string ExportPath = ConfigurationManager.AppSettings["csvExportPath"];
        static readonly string csvPath = ConfigurationManager.AppSettings["csvFilePath"];
        //static int LimitRowCount = int.Parse(ConfigurationManager.AppSettings["limitRowCount"]);
        static readonly string InsertUrl = ConfigurationManager.AppSettings["create"];
        static readonly string MutiInsertUrl = ConfigurationManager.AppSettings["mutiCreate"];
        static readonly string QueryUrl = ConfigurationManager.AppSettings["read"];
        static readonly string DeleteUrl = ConfigurationManager.AppSettings["delete"];
        static readonly string SummayUrl = ConfigurationManager.AppSettings["snownlp"];
        static readonly int MultipleRowNumber = Convert.ToInt32(ConfigurationManager.AppSettings["MultipleRowNumber"]);
        static readonly int MaxVerdictCount = Convert.ToInt32(ConfigurationManager.AppSettings["maxVerdictCount"]);
        static string _dirName = "", batchNumber = "";
        static int insertedRowCount = 0, batchCsvNumber = 1;

        static void Main(string[] args)
        {
            //Console.WriteLine($"Start to convert json file from {ZipFilePath}");
            logger.Info($"Start to convert json file from {ZipFilePath}");

            try
            {
                var tfidfList = GetTfidfModels();

                foreach (string file in Directory.EnumerateFiles(ZipFilePath, "*.zip"))
                {
                    using (ZipFile zip = ZipFile.Read(file))
                    {
                        logger.Info($"{zip.Name} is reading");
                        var verdictList = new List<VerdictModel>();

                        try
                        {
                            foreach (ZipEntry zipEntry in zip)
                            {
                                if (zipEntry.IsDirectory)
                                {
                                    _dirName = zipEntry.FileName.TrimEnd('/');
                                }
                                else if (zipEntry.IsText)
                                {
                                    if (insertedRowCount < MaxVerdictCount)
                                    {
                                        using (var ms = new MemoryStream())
                                        {
                                            zipEntry.Extract(ms);
                                            ms.Position = 0;
                                            var sr = new StreamReader(ms, Encoding.UTF8);
                                            var jsonString = sr.ReadToEnd();
                                            var model = JsonConvert.DeserializeObject<VerdictModel>(jsonString);

                                            if (model.date.HasValue && model.date.Value >= DateTime.Now.Date.AddYears(-2) && !verdictList.Any(o => o.no == model.no))
                                            {
                                                model.reason = RemoveWhiteSpace(model.reason);
                                                model.judgement = RemoveWhiteSpace(model.judgement);
                                                model.type = RemoveWhiteSpace(model.type);
                                                model.mainText = RemoveWhiteSpace(model.mainText);
                                                model.opinion = RemoveWhiteSpace(model.opinion);
                                                //model.tfidf = tfidfList.Any(t => t.no == model.no && t.system == model.sys && t.reason == model.reason) ?
                                                //    tfidfList.First(t => t.no == model.no && t.system == model.sys && t.reason == model.reason).variable : "";
                                                model.tfidf = tfidfList.Any(t => t.no_ == model.no && t.system == model.sys) ?
                                                    tfidfList.First(t => t.no_ == model.no && t.system == model.sys).variable : "";
                                                model.summary = GetSummay(new Summay { paramword = model.opinion });

                                                if (!string.IsNullOrEmpty(model.tfidf))
                                                    verdictList.Add(model);

                                                if (verdictList.Count() == MultipleRowNumber || zipEntry == zip.Last())
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
                                            }
                                            sr.Dispose();
                                            ms.Dispose();
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Warn($"Read {zip.Name} fail: {ex}");
                        }
                    }
                    //File.Delete($"{importPath}\\{dirName}");
                }
                //Console.WriteLine($"Export csv file done");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            //Console.WriteLine($"Convert to csv file done.");
            logger.Info("Convert to csv file done.");
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

        private static string RemoveWhiteSpace(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                str = Regex.Replace(str, @"\s+", "");
            }
            return str;
        }

        public static void ExportCSV(StringWriter csvString, string batchNumber)
        {
            File.WriteAllText($"{ExportPath}\\{_dirName}{batchNumber}.csv", csvString.ToString(), Encoding.UTF8);
            Console.WriteLine($"Export {insertedRowCount} verdicts in csv already.");
        }

        private static List<TfidfModel> GetTfidfModels()
        {
            logger.Info("Get TfidfModels");

            var tfidfs = new List<TfidfModel>();
            using (var reader = new StreamReader(csvPath))
            {
                using (var csv = new CsvReader(reader))
                {
                    using (var dr = new CsvDataReader(csv))
                    {
                        var dt = new DataTable();
                        dt.Load(dr);

                        var tfidfList = dt.AsEnumerable().Select(row =>
                        new TfidfModel
                        {
                            //article_id = row.Field<string>("article_id"),
                            variable = row.Field<string>("variable"),
                            //no = row.Field<string>("no"),
                            system = row.Field<string>("system"),
                            //reason = row.Field<string>("reason")
                            no_ = row.Field<string>("no_"),
                        }).ToList();

                        if (tfidfList.Count > 0)
                        {
                            //tfidfs = tfidfList.GroupBy(t => new { t.no, t.system, t.reason })
                            //.Select(g => new TfidfModel
                            //{
                            //    no = g.Key.no,
                            //    system = g.Key.system,
                            //    reason = g.Key.reason,
                            //    varibale = string.Join(",", g.Select(gg => gg.variable))
                            //});
                            var temp = tfidfList.GroupBy(t => new { t.no_, t.variable, t.system })
                                .Select(g => new TfidfModel
                                {
                                    no_ = g.Key.no_?.Replace('_', ','),
                                    system = g.Key.system,
                                    variable = g.Key.variable
                                });

                            tfidfs = temp.GroupBy(t => new { t.no_, t.system })
                            .Select(g => new TfidfModel
                            {
                                no_ = g.Key.no_?.Replace('_', ','),
                                system = g.Key.system,
                                variable = string.Join(",", g.Select(gg => gg.variable))
                            }).ToList();
                        }                       
                    }
                }
            }
            return tfidfs;
        }
        
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

        public static string GetSummay(Summay summay)
        {
            if (!string.IsNullOrEmpty(summay.paramword))
            {
                try
                {
                    //建立 WebRequest 並指定目標的 uri
                    WebRequest request = WebRequest.Create(SummayUrl);
                    //指定 request 使用的 http verb
                    request.Method = "POST";
                    //準備 post 用資料
                    var json = JsonConvert.SerializeObject(summay);
                    //指定 request 的 content type
                    request.ContentType = "application/json; charset=utf-8";
                    //指定 request header
                    request.Headers.Add("authorization", "token apikey");
                    //將需 post 的資料內容轉為 stream 
                    using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                    {
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
                                using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                                {
                                    summay.result = JsonConvert.DeserializeObject<Summay>(reader.ReadToEnd()).result;

                                    if (summay.result.Length > 0)
                                    {
                                        var result = "";

                                        foreach (var i in summay.result)
                                        {
                                            result += $"\"{i}\", ";
                                        }
                                        return result.TrimEnd().TrimEnd(',');
                                    }
                                }
                            }
                            else
                            {
                                logger.Debug($"Get summary fail: {json}");
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
            }
            return summay.paramword;
        }

        public class VerdictModel
        {
            public string court { get; set; }
            public DateTime? date { get; set; }
            public string no { get; set; }
            public string sys { get; set; }
            public string reason { get; set; }
            public string judgement { get; set; }
            public string type { get; set; }
            public string mainText { get; set; }
            public string opinion { get; set; }
            public string tfidf { get; set; }
            public string summary { get; set; }

            public class RelatedIssues
            {
                public string lawName { get; set; }
                public string issueRef { get; set; }
            }
            public List<RelatedIssues> relatedIssues { get; set; }

            //public class Party
            //{
            //    public string title { get; set; }
            //    public string value { get; set; }
            //}
            //public List<Party> party { get; set; }
        }

        public class TfidfModel
        {
            //no_,variable,tf_idf,rank,system,reason
            //public string article_id { get; set; }
            //public string variable { get; set; }
            //public string no { get; set; }
            public string system { get; set; }
            //public string reason { get; set; }
            public string no_ { get; set; }
            public string variable { get; set; }
        }

        public class Condition
        {
            public string no { get; set; }
            public string sys { get; set; }
            public string reason { get; set; }
        }

        public class Summay
        {
            public string paramword { get; set; }
            public string[] result { get; set; }
        }
    }
}
