using CommandLine;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace MangaReader
{
    internal class Options
    {
        [Option('b', "bulk-json-path", Default = "", HelpText = "If you want to add a bunch of JSON files at once.")]
        public string BulkJSONPath { get; set; }

        [Option('c', "chapter-number", Default = -1, HelpText = "The chapter number you're scanning.  Can be a double.  Required if input is not a chapter JSON.")]
        public double ChapterNumber { get; set; }

        [Option('i', "input-path", Default = "", HelpText = "The path containing the chapters you wish to OCR or a JSON file representing a OCR'd chapter.  If the former, ensure that all pages are in order and are JPGs or PNGs.")]
        public string InputPath { get; set; }

        [Option('l', "logging", Default = false, HelpText = "Whether or not to enable logging.  Stored at the program's directory.")]
        public bool Logging { get; set; }

        [Option('m', "master-dictionary-filepath", Default = "", HelpText = "The filepath where you want the master dictionary file to be appended to.  If left blank or if the file does not exist, a new one is generated from scratch at the same directory as the input file")]
        public string MasterDictionaryFilePath { get; set; }

        [Option('o', "output-json-filepath", Default = "", HelpText = "The filepath where you wish your output JSON file to be placed if you try to OCR.  If not specified, this is just the same directory as the input file.")]
        public string OutputFilePath { get; set; }

        [Option('v', "verbose", Default = false, HelpText = "Whether or not to output to console.")]
        public bool Verbose { get; set; }

        [Option('t', "time", Default = 10, HelpText = "How long to wait in between endpoint calls in seconds.  Defaults to 10 seconds.")]
        public int Time { get; set; }

        [Option('r', "regenerate", Default = false, HelpText = "Whether to regenerate existing chapter files.")]
        public bool Regenerate { get; set; }

        [Option('u', "bulk-convert", Default = "", HelpText = "The directory in which the program will convert the first and deepest folder's contents for each immediate folder in this path.  Note this mode is tailored for guya.moe files, and thus, requires specific naming conventions for the auto-chapter numbering.  For example, it expects that the immediate folder contains a chapter number format regex of the form '[0-9]+(?:-[0-9]*)?_.*'")]
        public string BulkConvertPath { get; set; }
    }

    internal class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static string endpoint;
        private static string subscriptionKey;

        private static void Main(string[] args)
        {
            NLog.Config.LoggingConfiguration config = new NLog.Config.LoggingConfiguration();
            var options = new Options();
            string inputPath = "";
            string outputFile = "";
            string masterFile = "";
            string bulkFolderDirectory = "";
            double chapterNum = -1;
            string bulkJSONPath = "";
            int endpointTime = 10;
            bool toRegenerate = false;

            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                if (o.Logging)
                {
                    NLog.Targets.FileTarget logfile = new NLog.Targets.FileTarget("logfile") { FileName = "diagnostics.log" };
                    config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logfile);
                }

                if (o.Verbose)
                {
                    NLog.Targets.ConsoleTarget logconsole = new NLog.Targets.ConsoleTarget("logconsole");
                    config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logconsole);
                }
                NLog.LogManager.Configuration = config;

                inputPath = o.InputPath;
                outputFile = o.OutputFilePath;
                masterFile = o.MasterDictionaryFilePath;
                chapterNum = o.ChapterNumber;
                bulkJSONPath = o.BulkJSONPath;
                endpointTime = o.Time;
                toRegenerate = o.Regenerate;
                bulkFolderDirectory = o.BulkConvertPath;

                // First read JSON, error out if non-existant
                if (!File.Exists("config.json"))
                {
                    logger.Error("Missing config.json file!");
                    return;
                }
                string configFileText = File.ReadAllText("config.json");
                try
                {
                    JObject configJSON = JObject.Parse(configFileText);
                    subscriptionKey = configJSON.SelectToken("subscriptionKey").ToString();
                    endpoint = configJSON.SelectToken("personalEndpoint").ToString();
                    logger.Info(string.Format("SubKey: {0}, Endpoint: {1}", subscriptionKey, endpoint));
                }
                catch (System.Exception ex)
                {
                    logger.Error(string.Format("Error while reading config.json file: " + ex));
                    return;
                }

                if (inputPath.Equals("") && bulkJSONPath.Equals("") && bulkFolderDirectory.Equals(""))
                {
                    logger.Error("Please include at least one valid form of input!");
                    return;
                }

                if (outputFile.Equals(""))
                {
                    outputFile = Path.GetDirectoryName(inputPath) + "/" + chapterNum + ".json";
                }

                if (masterFile.Equals(""))
                {
                    if (!inputPath.Equals(""))
                    {
                        masterFile = Path.GetDirectoryName(inputPath) + "/" + "master_dictionary" + ".json";
                    }
                    else if (!bulkJSONPath.Equals(""))
                    {
                        masterFile = new DirectoryInfo(Path.GetDirectoryName(bulkJSONPath)).FullName + "/" + "master_dictionary" + ".json";
                    }
                    else
                    {
                        masterFile = new DirectoryInfo(Path.GetDirectoryName(bulkFolderDirectory)).FullName + "/" + "master_dictionary" + ".json";
                    }
                }

                JSONGenerator jsonGen = new JSONGenerator(logger);

                // TODO: Add a capability to remove a chapter's OCR from the master.
                // Decide what to run

                if (!bulkFolderDirectory.Equals(""))
                {
                    if (Directory.Exists(bulkFolderDirectory))
                    {
                        logger.Debug("Master folder for bulk: {0}", masterFile);
                        // Just use this and bail...
                        foreach (var folder in Directory.EnumerateDirectories(bulkFolderDirectory))
                        {
                            // For each folder:

                            var splitNum = new DirectoryInfo(folder).Name.Split('_')[0].Replace("-", ".");
                            logger.Debug("SplitNum: {0}", splitNum);
                            var autoChapterNum = Convert.ToDouble(splitNum);
                            logger.Debug("Found chapter: {0}", autoChapterNum);

                            // Move to the last directory possible
                            var autoInputPath = folder;
                            var enumedDirs = Directory.EnumerateDirectories(autoInputPath);
                            while (enumedDirs != null && enumedDirs.Any())
                            {
                                autoInputPath = enumedDirs.First();
                                enumedDirs = Directory.EnumerateDirectories(autoInputPath);
                            }
                            logger.Debug("Converting folder: {0}", autoInputPath);

                            outputFile = new DirectoryInfo(Path.GetDirectoryName(bulkFolderDirectory)).FullName + "/" + splitNum + "-dict.json";
                            logger.Debug("Saving output to: {0}", outputFile);

                            // Convert!
                            OCRScanner ocrScanner = new OCRScanner(logger, subscriptionKey, endpoint, endpointTime);
                            var tokens = ocrScanner.GenerateOCR(autoInputPath, autoChapterNum);
                            jsonGen.FormatJSON(tokens, outputFile, masterFile, autoChapterNum);
                        }
                    }
                    else
                    {
                        logger.Error("Invalid bulk folder directory!");
                    }
                    return;
                }

                if (!inputPath.Equals("") && (!File.Exists(inputPath) && !Directory.Exists(inputPath)))
                {
                    logger.Error("Input file path is invalid!");
                }
                else if (inputPath.Equals("") && !File.Exists(bulkJSONPath) && !Directory.Exists(bulkJSONPath))
                {
                    logger.Error("Bulk directory path is invalid!");
                }
                else if (!bulkJSONPath.Equals(""))
                {
                    if (toRegenerate)
                    {
                        jsonGen.RegenerateExistingJSON(bulkJSONPath, masterFile);
                    }
                    else
                    {
                        jsonGen.BulkAddJSONToDictionary(bulkJSONPath, masterFile);
                    }
                }
                else if (!inputPath.EndsWith(".json"))
                {
                    if (chapterNum < 0)
                    {
                        logger.Error("Chapter number is missing!");
                    }
                    else
                    {
                        OCRScanner ocrScanner = new OCRScanner(logger, subscriptionKey, endpoint, endpointTime);
                        var tokens = ocrScanner.GenerateOCR(inputPath, chapterNum);
                        jsonGen.FormatJSON(tokens, outputFile, masterFile, chapterNum);
                    }
                }
                else
                {
                    jsonGen.AddJSONToDictionary(inputPath, masterFile);
                }
            });
        }
    }
}
