using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MangaReader
{
    internal class JSONGenerator
    {
        private readonly NLog.Logger logger;

        public JSONGenerator(NLog.Logger logger)
        {
            this.logger = logger;
        }

        public void BulkAddJSONToDictionary(string jsonFileDir, string masterFile)
        {
            if (Directory.Exists(jsonFileDir))
            {
                List<string> jsonPaths = new List<string>(Directory.EnumerateFiles(jsonFileDir));
                foreach(var jsonPath in jsonPaths)
                {
                    AddJSONToDictionary(jsonPath, masterFile);
                }
            }
            else
            {
                logger.Error("Directory does not exist.");
            }
        }

        public void AddJSONToDictionary(string chapterJSONFile, string masterFile)
        {
            MasterDictionary masterDictionary = new MasterDictionary();
            if (File.Exists(masterFile))
            {
                try
                {
                    masterDictionary = JsonConvert.DeserializeObject<MasterDictionary>(File.ReadAllText(masterFile));
                }
                catch
                {
                    logger.Error(string.Format("Unable to open the master dictionary at {0}.  Halting.", masterFile));
                }
            }
            else
            {
                masterDictionary = new MasterDictionary
                {
                    MentionedWordLocation = new SortedDictionary<string, SortedDictionary<string, List<int>>>()
                };
                logger.Info("Created new dictionary.");
            }

            if (!File.Exists(chapterJSONFile))
            {
                logger.Error("File was missing.");
                return;
            }


            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            ChapterJSON formattedChapter = JsonConvert.DeserializeObject<ChapterJSON>(File.ReadAllText(chapterJSONFile));
            double chapterNum = formattedChapter.Chapter;
            logger.Info(string.Format("Formatting chapter {0}.", chapterNum));
            string chapterNumString = chapterNum.ToString().Replace(".", "-");
            foreach (var wordEntry in formattedChapter.MentionedWordChapterLocation)
            {
                var pageList = wordEntry.Value;
                var upperWord = wordEntry.Key;

                if (masterDictionary.MentionedWordLocation.ContainsKey(upperWord))
                {
                    if (!masterDictionary.MentionedWordLocation[upperWord].ContainsKey(chapterNumString))
                    {
                        // Chapter dictionary DNE!
                        masterDictionary.MentionedWordLocation[upperWord].Add(chapterNumString, pageList);
                    }
                    else
                    {
                        foreach(var pageNum in pageList)
                        {
                            if (!masterDictionary.MentionedWordLocation[upperWord][chapterNumString].Contains(pageNum))
                            {
                                masterDictionary.MentionedWordLocation[upperWord][chapterNumString].Add(pageNum);
                            }
                        }
                    }
                }
                else
                {
                    // Word DNE in dictionary
                    var newChapterDict = new SortedDictionary<string, List<int>>
                    {
                        { chapterNumString, pageList }
                    };
                    masterDictionary.MentionedWordLocation.Add(upperWord, newChapterDict);
                }
            }

            generateMasterJSON(masterFile, masterDictionary);

            stopwatch.Stop();
            logger.Debug(string.Format("Total time for dictionary: {0} seconds.", stopwatch.ElapsedMilliseconds / 1000));
        }

        public void FormatJSON(List<JToken> inputJTokens, string outputFile, string masterFile, double chapterNum)
        {
            MasterDictionary masterDictionary = new MasterDictionary();
            if (File.Exists(masterFile))
            {
                try
                {
                    masterDictionary = JsonConvert.DeserializeObject<MasterDictionary>(File.ReadAllText(masterFile));
                }
                catch
                {
                    logger.Error(string.Format("Unable to open the master dictionary at {0}.  Halting.", masterFile));
                }
            }
            else
            {
                masterDictionary = new MasterDictionary
                {
                    MentionedWordLocation = new SortedDictionary<string, SortedDictionary<string, List<int>>>()
                };
            }

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            logger.Info(string.Format("Formatting chapter {0}.", chapterNum));
            int fileNum = 0;
            int totalJSONFiles = inputJTokens.Count;

            ChapterJSON chapterJSON = new ChapterJSON
            {
                Chapter = chapterNum,
                Pages = new List<FormattedJSONPage>(),
                MentionedWordChapterLocation = new Dictionary<string, List<int>>()
            };
            foreach (var token in inputJTokens)
            {
                ++fileNum;
                // Deserialize JSON file
                string fileText = token.ToString();
                try
                {
                    ResponseJSON responseJSON = JsonConvert.DeserializeObject<List<ResponseJSON>>(fileText)[0];

                    FormattedJSONPage formattedPage = new FormattedJSONPage
                    {
                        Width = responseJSON.Width,
                        Height = responseJSON.Height,
                        Words = new List<FormattedWord>(),
                        Page = fileNum  // We are *going* to trust that that fileNum === pageNum... even though it could easily not be true lol
                    };

                    foreach (var line in responseJSON.Lines)
                    {
                        FormattedWord formattedWord = new FormattedWord
                        {
                            Text = line.Text,
                            BoundingBox = new List<Coords>()
                        };
                        for (int i = 0; i < 7; i += 2)
                        {
                            formattedWord.BoundingBox.Add(new Coords { X = line.BoundingBox.ElementAt(i), Y = line.BoundingBox.ElementAt(i + 1) });
                        }
                        formattedPage.Words.Add(formattedWord);

                        // In addition, scan words and add to map if needed
                        foreach (var word in line.Words)
                        {
                            var regexedWord = Regex.Replace(word.Text, @"([.,;!?""]|[-'#]$)", "");
                            regexedWord = Regex.Replace(regexedWord, @"([.,;!?""]|[-'#]$)", "");
                            var upperWord = regexedWord.ToUpper();

                            if (upperWord.Length > 0)
                            {
                                if (chapterJSON.MentionedWordChapterLocation.ContainsKey(upperWord))
                                {
                                    if (!chapterJSON.MentionedWordChapterLocation[upperWord].Contains(fileNum))
                                    {
                                        chapterJSON.MentionedWordChapterLocation[upperWord].Add(fileNum);
                                    }
                                }
                                else
                                {
                                    var newListOfPages = new List<int>
                                    {
                                        fileNum
                                    };
                                    chapterJSON.MentionedWordChapterLocation.Add(upperWord, newListOfPages);
                                }

                                string chapterNumString = chapterNum.ToString().Replace(".", "-");
                                if (masterDictionary.MentionedWordLocation.ContainsKey(upperWord))
                                {
                                    if (!masterDictionary.MentionedWordLocation[upperWord].ContainsKey(chapterNumString))
                                    {
                                        // Chapter dictionary DNE!
                                        var newPageList = new List<int>
                                        {
                                            fileNum
                                        };
                                        masterDictionary.MentionedWordLocation[upperWord].Add(chapterNumString, newPageList);
                                    }
                                    else
                                    {
                                        // Ensure that entry is not inserted multiple times
                                        if (!masterDictionary.MentionedWordLocation[upperWord][chapterNumString].Contains(fileNum))
                                        {
                                            masterDictionary.MentionedWordLocation[upperWord][chapterNumString].Add(fileNum);
                                        }
                                    }
                                }
                                else
                                {
                                    // Word DNE in dictionary
                                    var newChapterDict = new SortedDictionary<string, List<int>>();
                                    var newPageList = new List<int>
                                    {
                                        fileNum
                                    };
                                    newChapterDict.Add(chapterNumString, newPageList);
                                    masterDictionary.MentionedWordLocation.Add(upperWord, newChapterDict);
                                }
                            }
                        }
                    }
                    chapterJSON.Pages.Add(formattedPage);
                }
                catch (Exception ex)
                {
                    logger.Error(string.Format("Skipping file {0} of {1}, encountered exception: {2}", fileNum, totalJSONFiles, ex.Message));
                }
            }

            // Write to chapter JSON
            try {
                File.WriteAllText(outputFile, JsonConvert.SerializeObject(chapterJSON, Formatting.Indented));
                generateMasterJSON(masterFile, masterDictionary);
            }
            catch (Exception ex) {
                logger.Error(string.Format("Error while writing files: " + ex));
            }

            stopwatch.Stop();
            logger.Debug(string.Format("Total time for generating json for chapter: {0} seconds.", stopwatch.ElapsedMilliseconds / 1000));
        }

        private void generateMasterJSON(string masterFile, MasterDictionary masterDictionary)
        {
            // Generate master dictionary JSON file
            logger.Info("Generating word dictionary.");
            System.Diagnostics.Stopwatch dictStopWatch = new System.Diagnostics.Stopwatch();
            dictStopWatch.Start();

            File.WriteAllText(masterFile, JsonConvert.SerializeObject(masterDictionary, Formatting.Indented));

            dictStopWatch.Stop();
            if (dictStopWatch.ElapsedMilliseconds / 1000 > 0) logger.Debug(string.Format("Total time for word dictionary generation: {0} seconds.", dictStopWatch.ElapsedMilliseconds / 1000));
        }
    }
}