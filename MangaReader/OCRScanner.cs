using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace MangaReader
{
    internal class OCRScanner
    {
        private readonly string endpoint;
        private readonly int endpointTime;
        private readonly NLog.Logger logger;
        private readonly string subscriptionKey;
        public OCRScanner(NLog.Logger logger, string subscriptionKey, string endpoint, int endpointTime)
        {
            this.logger = logger;
            this.subscriptionKey = subscriptionKey;
            this.endpoint = endpoint + "/vision/v2.0/read/core/asyncBatchAnalyze";
            this.endpointTime = endpointTime;
        }

        public List<JToken> GenerateOCR(string inputDir, double chapterNum)
        {
            List<JToken> listOfJTokens = new List<JToken>();
            string[] supportedExtensions = { ".png", ".jpg" };
            List<string> imagePaths = new List<string>(Directory.EnumerateFiles(inputDir, "*.*", SearchOption.TopDirectoryOnly)
                                                        .Where(s => supportedExtensions.Any(ext => ext == Path.GetExtension(s)))).OrderBy(q => q).ToList();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            logger.Info(string.Format("Scanning chapter {0}.", chapterNum));
            int pageNum = 0;
            int totalPages = imagePaths.Count;
            foreach (var imagePath in imagePaths)
            {
                ++pageNum;
                System.Diagnostics.Stopwatch pageStopWatch = new System.Diagnostics.Stopwatch();
                pageStopWatch.Start();
                logger.Info(string.Format("Scanning page {0} of {1}", pageNum, totalPages));
                logger.Info(string.Format("Page path: {0}", imagePath));

                var currentPageToken = CallReadAPI(imagePath).Result;
                if (currentPageToken != null)
                {
                    listOfJTokens.Add(currentPageToken);
                }

                pageStopWatch.Stop();
                logger.Debug(string.Format("Total time for chapter: {0} seconds.", pageStopWatch.ElapsedMilliseconds / 1000));
            }
            stopwatch.Stop();
            logger.Debug(string.Format("Total time for chapter: {0} seconds.", stopwatch.ElapsedMilliseconds / 1000));

            return listOfJTokens;
        }

        private async Task<JToken> CallReadAPI(string imageFilePath)
        {
            if (File.Exists(imageFilePath))
            {
                try
                {
                    string uri = endpoint + "?";

                    FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
                    BinaryReader binaryReader = new BinaryReader(fileStream);
                    byte[] byteArray = binaryReader.ReadBytes((int)fileStream.Length);
                    fileStream.Close();
                    binaryReader.Close();

                    int count = 0;
                    while (true)
                    {
                        HttpClient httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                        ByteArrayContent byteContent = new ByteArrayContent(byteArray);
                        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                        HttpResponseMessage response = await httpClient.PostAsync(uri, byteContent);
                        logger.Info(string.Format("POST response code: {0}", response.StatusCode));
                        httpClient.Dispose();

                        if (response.IsSuccessStatusCode)
                        {
                            response.Dispose();
                            HttpResponseHeaders results = response.Headers;
                            if (response.Headers.TryGetValues("Operation-Location", out IEnumerable<string> values))
                            {
                                string resultURI = values.First();

                                while (true)
                                {
                                    Thread.Sleep(endpointTime * 1000 / 2);
                                    HttpClient readJSONClient = new HttpClient();
                                    readJSONClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                                    HttpResponseMessage jsonResponse = await readJSONClient.GetAsync(resultURI);
                                    Thread.Sleep(endpointTime * 1000 / 2);
                                    if (jsonResponse.IsSuccessStatusCode || jsonResponse.StatusCode.Equals("Accepted"))
                                    {
                                        string resultingJSONString = await jsonResponse.Content.ReadAsStringAsync();
                                        JObject resultJSON = JObject.Parse(resultingJSONString);

                                        resultJSON.TryGetValue("status", out JToken resultStatus);
                                        logger.Info(string.Format("Current scanning status: {0}", resultStatus));
                                        jsonResponse.Dispose();

                                        if (resultStatus != null && resultStatus.ToString() != "Running")
                                        {
                                            resultJSON.TryGetValue("recognitionResults", out JToken recognitionResults);
                                            readJSONClient.Dispose();
                                            return recognitionResults;
                                        }
                                    }
                                    else
                                    {
                                        jsonResponse.Dispose();
                                        logger.Debug(string.Format("Recieved a non-successful status code while GETing: {0}", response.StatusCode));
                                    }
                                    readJSONClient.Dispose();
                                }
                            }
                        }
                        else
                        {
                            response.Dispose();
                            logger.Debug(string.Format("Recieved a non-successful status code while POSTing: {0}", response.StatusCode));
                            ++count;
                            if (count >= 5)
                            {
                                logger.Debug(string.Format("Failed to set POST {0} times, aborting this send.", count));
                                return null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug(string.Format("Error, caught exception: " + ex));
                }
            }
            else
            {
                logger.Debug("Invalid file path, does not exist.");
            }

            return null;
        }
    }
}
