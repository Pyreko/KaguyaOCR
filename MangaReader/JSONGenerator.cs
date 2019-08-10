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

        public void AddJSONToDictionary(string chapterJSONFile, string masterFilePath)
        {
            if (!File.Exists(chapterJSONFile))
            {
                logger.Error("File was missing.");
                return;
            }

            ChapterJSON chapterJSON = JsonConvert.DeserializeObject<ChapterJSON>(File.ReadAllText(chapterJSONFile));
            AddExistingChapterJSONToMasterDictionary(chapterJSON, masterFilePath);
        }

        public void BulkAddJSONToDictionary(string jsonFileDir, string masterFile)
        {
            if (Directory.Exists(jsonFileDir))
            {
                List<string> jsonPaths = new List<string>(Directory.EnumerateFiles(jsonFileDir));
                foreach (var jsonPath in jsonPaths)
                {
                    AddJSONToDictionary(jsonPath, masterFile);
                }
            }
            else
            {
                logger.Error("Directory does not exist.");
            }
        }

        public void FormatJSON(List<JToken> inputJTokens, string outputFile, string masterFilePath, double chapterNum)
        {
            MasterDictionary masterDictionary;

            if (File.Exists(masterFilePath))
            {
                try
                {
                    masterDictionary = JsonConvert.DeserializeObject<MasterDictionary>(File.ReadAllText(masterFilePath));
                }
                catch
                {
                    logger.Error(string.Format("Unable to open the master dictionary at {0}.  Halting.", masterFilePath));
                }
            }

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

                        if (line.Text.EndsWith('-') || line.Text.StartsWith('-'))
                        {
                            FixHyphens(chapterJSON, responseJSON.Lines, line, fileNum);
                        }

                        // In addition, scan words and add to map if needed
                        foreach (var word in line.Words)
                        {
                            var upperWord = RegexFormatAndToUpperWord(word.Text);
                            upperWord = AutoCorrectList(upperWord);

                            if (upperWord.Length > 0)
                            {
                                InsertWordToChapterDict(upperWord, fileNum, chapterJSON);
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
            try
            {
                File.WriteAllText(outputFile, JsonConvert.SerializeObject(chapterJSON, Formatting.Indented), System.Text.Encoding.UTF8);
                AddExistingChapterJSONToMasterDictionary(chapterJSON, masterFilePath);
            }
            catch (Exception ex)
            {
                logger.Error(string.Format("Error while writing files: " + ex));
            }
        }

        /// <summary>
        /// Used to update existing chapter directories and thus the master dictionary after I update functionality.
        /// The way this works is it basically destroys the chapter's existing master dict and regens it using the (newer) existing rules.
        /// </summary>
        /// <param name="jsonFileDir"></param>
        /// <param name="masterFile"></param>
        public void RegenerateExistingJSON(string jsonFileDir, string masterFile)
        {
            if (Directory.Exists(jsonFileDir))
            {
                List<string> jsonPaths = new List<string>(Directory.EnumerateFiles(jsonFileDir));
                File.Delete(masterFile);

                foreach (var jsonPath in jsonPaths)
                {
                    ChapterJSON chapterJSON = JsonConvert.DeserializeObject<ChapterJSON>(File.ReadAllText(jsonPath));
                    RegenerateJSON(jsonPath, chapterJSON);
                }
                BulkAddJSONToDictionary(jsonFileDir, masterFile);
            }
            else
            {
                logger.Error("Directory does not exist.");
            }
        }

        private string AutoCorrectList(string stringToCheck)
        {
            // Main idea is to check words that are on this list and autocorrect them.  For now this is basic, it may get more complex...

            switch(stringToCheck)
            {
                case "NFORMATION":
                    return "INFORMATION";
                case "NG":
                    return "ING";
                case "NGS":
                    return "INGS";
                case "KUIN":
                case "KLIN":
                    return "KUN";
                default:
                    return stringToCheck;
            }

        }

        private void AddExistingChapterJSONToMasterDictionary(ChapterJSON chapterJSON, string masterFilePath)
        {
            MasterDictionary masterDictionary = new MasterDictionary();
            if (File.Exists(masterFilePath))
            {
                try
                {
                    masterDictionary = JsonConvert.DeserializeObject<MasterDictionary>(File.ReadAllText(masterFilePath));
                }
                catch
                {
                    logger.Error(string.Format("Unable to open the master dictionary at {0}.  Halting.", masterFilePath));
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

            double chapterNum = chapterJSON.Chapter;
            logger.Info(string.Format("Formatting chapter {0}.", chapterNum));
            string chapterNumString = chapterNum.ToString().Replace(".", "-");
            foreach (var wordEntry in chapterJSON.MentionedWordChapterLocation)
            {
                var pageList = wordEntry.Value;
                var upperWord = wordEntry.Key;
                upperWord = AutoCorrectList(upperWord);

                if (masterDictionary.MentionedWordLocation.ContainsKey(upperWord))
                {
                    if (!masterDictionary.MentionedWordLocation[upperWord].ContainsKey(chapterNumString))
                    {
                        // Chapter dictionary DNE!
                        masterDictionary.MentionedWordLocation[upperWord].Add(chapterNumString, pageList);
                    }
                    else
                    {
                        foreach (var pageNum in pageList)
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

            GenerateMasterJSON(masterFilePath, masterDictionary);
        }

        private bool DoIntersect(Coords aTopLeft, Coords aBottomRight, Coords bTopLeft, Coords bBottomRight, int heightOffset)
        {
            heightOffset /= 2;
            logger.Debug("{0}, {1}, {2}, {3}", bTopLeft.X > aBottomRight.X, bBottomRight.X < aTopLeft.X, bTopLeft.Y - heightOffset > aBottomRight.Y, bBottomRight.Y < aTopLeft.Y - heightOffset);
            logger.Debug("{0} > {1}?", aTopLeft.X, bBottomRight.X);
            return !(bTopLeft.X > aBottomRight.X || aTopLeft.X > bBottomRight.X || bTopLeft.Y - heightOffset > aBottomRight.Y || aTopLeft.Y - heightOffset > bBottomRight.Y);
        }

        private void FixHyphens(ChapterJSON chapterJSON, FormattedWord hyphenedFormattedWord, int fileNum)
        {
            Console.WriteLine("Hyphened word: " + hyphenedFormattedWord.Text);
            if (!(hyphenedFormattedWord.Text.EndsWith('-') || hyphenedFormattedWord.Text.StartsWith('-')))
            {
                return;
            }

            IReadOnlyList<string> HAS_HYPHEN_LIST = new List<string> { "SAMA", "SAN", "SENSEI", "KUN", "CHAN", "SENPAI" };
            IReadOnlyList<string> NO_HYPHEN_LIST = new List<string> { "ING", "INGS" };


            foreach (var candidateLine in chapterJSON.Pages[fileNum - 1].Words)
            {
                // We skip if the coords are the same... as then the line we're looking at is equal to the hyphen line
                if (candidateLine.BoundingBox[0] != hyphenedFormattedWord.BoundingBox[0] && candidateLine.BoundingBox[2] != hyphenedFormattedWord.BoundingBox[2])
                {
                    if (hyphenedFormattedWord.Text.EndsWith('-'))
                    {
                        if (hyphenedFormattedWord.BoundingBox[0].Y < candidateLine.BoundingBox[0].Y)
                        {
                            logger.Debug("Word: " + candidateLine.Text);
                            if (DoIntersect(hyphenedFormattedWord.BoundingBox[0], hyphenedFormattedWord.BoundingBox[2], candidateLine.BoundingBox[0], candidateLine.BoundingBox[2], Math.Abs(hyphenedFormattedWord.BoundingBox[0].Y - hyphenedFormattedWord.BoundingBox[3].Y)))
                            {
                                if (!NO_HYPHEN_LIST.Contains(AutoCorrectList(TrimAllHyphens(RegexFormatAndToUpperWord(candidateLine.Text.Split(' ').First())))))
                                {
                                    string hyphenedWord = AutoCorrectList(hyphenedFormattedWord.Text.Split(' ').Last()) + AutoCorrectList(RegexFormatAndToUpperWord(candidateLine.Text.Split(' ').First()));
                                    string hyphenedUpperWord = RegexFormatAndToUpperWord(hyphenedWord);
                                    if (hyphenedUpperWord.Length > 0)
                                    {
                                        Console.WriteLine("Inserting: " + hyphenedUpperWord);
                                        InsertWordToChapterDict(hyphenedUpperWord, fileNum, chapterJSON);
                                    }
                                }

                                // Skip if the word MUST contain a hyphen!
                                if (!HAS_HYPHEN_LIST.Contains(AutoCorrectList(TrimAllHyphens(RegexFormatAndToUpperWord(candidateLine.Text.Split(' ').First())))))
                                {
                                    string unHyphenedWord = AutoCorrectList(RegexFormatAndToUpperWord(hyphenedFormattedWord.Text.Split(' ').Last())) + AutoCorrectList(RegexFormatAndToUpperWord(candidateLine.Text.Split(' ').First()));
                                    string unHyphenedUpperWord = RegexFormatAndToUpperWord(unHyphenedWord);

                                    if (unHyphenedUpperWord.Length > 0)
                                    {
                                        Console.WriteLine("Inserting no-hyphen: " + unHyphenedUpperWord);
                                        InsertWordToChapterDict(unHyphenedUpperWord, fileNum, chapterJSON);
                                    }
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        if (hyphenedFormattedWord.BoundingBox[0].Y > candidateLine.BoundingBox[0].Y)
                        {
                            if (DoIntersect(candidateLine.BoundingBox[0], candidateLine.BoundingBox[2], hyphenedFormattedWord.BoundingBox[0], hyphenedFormattedWord.BoundingBox[2], Math.Abs(hyphenedFormattedWord.BoundingBox[0].Y - hyphenedFormattedWord.BoundingBox[3].Y)))
                            {
                                if (!NO_HYPHEN_LIST.Contains(AutoCorrectList(TrimAllHyphens(RegexFormatAndToUpperWord(hyphenedFormattedWord.Text.Split(' ').First())))))
                                {
                                    string hyphenedWord = AutoCorrectList(candidateLine.Text.Split(' ').Last()) + AutoCorrectList(RegexFormatAndToUpperWord(hyphenedFormattedWord.Text.Split(' ').First()));
                                    string hyphenedUpperWord = RegexFormatAndToUpperWord(hyphenedWord);
                                    if (hyphenedUpperWord.Length > 0)
                                    {
                                        Console.WriteLine("Inserting: " + hyphenedUpperWord);
                                        InsertWordToChapterDict(hyphenedUpperWord, fileNum, chapterJSON);
                                    }
                                }

                                // Skip if the word MUST contain a hyphen!
                                if (!HAS_HYPHEN_LIST.Contains(AutoCorrectList(TrimAllHyphens(RegexFormatAndToUpperWord(hyphenedFormattedWord.Text.Split(' ').First())))))
                                {
                                    string unHyphenedWord = AutoCorrectList(RegexFormatAndToUpperWord(candidateLine.Text.Split(' ').Last())) + AutoCorrectList(TrimAllHyphens(RegexFormatAndToUpperWord(hyphenedFormattedWord.Text.Split(' ').First())));
                                    string unHyphenedUpperWord = RegexFormatAndToUpperWord(unHyphenedWord);

                                    if (unHyphenedUpperWord.Length > 0)
                                    {
                                        Console.WriteLine("Inserting: " + unHyphenedUpperWord);
                                        InsertWordToChapterDict(unHyphenedUpperWord, fileNum, chapterJSON);
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }

        private void FixHyphens(ChapterJSON chapterJSON, List<Line> lines, Line hyphenedLine, int fileNum)
        {
            /*
             * The way this works is the following:
                * Check for nearest bounding box in the page that has a left upper corner that is greater the y of the left lower corner (candidate box)
                * Check if the two bounding boxes intersect, if the bounding box y of the upper corners are raised for the candidate box.  We check
                  by adding the total height of the box we are checking *against*.
                * If there is a match, then halt(?).  Proceed to insert the following entries to the master and chapter dictionaries:
                    * Last word of the line **with** the hyphen, then followed by the first word in the candidate box appended.
                    * Last word on the line **without** the hyphen, then followed by the first word in the candidate box appended.  This will NOT occur
                      if the first word is whitelisted to alway exist with a hyphen in NO_HYPHEN_LIST.
                * Note that the word by itself with no hyphen will already be covered by the main program.
             */

            IReadOnlyList<string> HAS_HYPHEN_LIST = new List<string> { "SAMA", "SAN", "SENSEI", "KUN", "CHAN", "BO", "SENPAI" };
            IReadOnlyList<string> NO_HYPHEN_LIST = new List<string> { "ING", "INGS" };

            if (hyphenedLine.Text.EndsWith('-'))
            {
                if (hyphenedLine.BoundingBox.Count == 8)
                {
                    var hyphenLineTopLeftCoords = new Coords { X = hyphenedLine.BoundingBox.ElementAt(0), Y = hyphenedLine.BoundingBox.ElementAt(1) };
                    var hyphenLineBottomRightCoords = new Coords { X = hyphenedLine.BoundingBox.ElementAt(4), Y = hyphenedLine.BoundingBox.ElementAt(5) };

                    foreach (var candidateLine in lines)
                    {
                        if (candidateLine.BoundingBox.Count == 8)
                        {
                            var candidateLineTopLeftCoords = new Coords { X = candidateLine.BoundingBox.ElementAt(0), Y = candidateLine.BoundingBox.ElementAt(1) };
                            var candidateLineBottomRightCoords = new Coords { X = candidateLine.BoundingBox.ElementAt(4), Y = candidateLine.BoundingBox.ElementAt(5) };

                            // We skip if the coords are the same... as then the line we're looking at is equal to the hyphen line
                            if (candidateLineTopLeftCoords != hyphenLineTopLeftCoords)
                            {
                                if (hyphenLineTopLeftCoords.Y < candidateLineTopLeftCoords.Y)
                                {
                                    if (DoIntersect(hyphenLineTopLeftCoords, hyphenLineBottomRightCoords, candidateLineTopLeftCoords, candidateLineBottomRightCoords, Math.Abs(hyphenLineTopLeftCoords.Y - hyphenLineBottomRightCoords.Y)))
                                    {
                                        string hyphenedWord = hyphenedLine.Words.Last().Text + AutoCorrectList(candidateLine.Words.First().Text);
                                        string hyphenedUpperWord = RegexFormatAndToUpperWord(hyphenedWord);
                                        if (hyphenedUpperWord.Length > 0)
                                        {
                                            InsertWordToChapterDict(hyphenedUpperWord, fileNum, chapterJSON);
                                        }

                                        // Skip if the word MUST contain a hyphen!
                                        if (!HAS_HYPHEN_LIST.Contains(RegexFormatAndToUpperWord(candidateLine.Words.First().Text.ToUpper())))
                                        {
                                            string unHyphenedWord = AutoCorrectList(RegexFormatAndToUpperWord(hyphenedLine.Words.Last().Text)) + AutoCorrectList(RegexFormatAndToUpperWord(candidateLine.Words.First().Text));
                                            string unHyphenedUpperWord = RegexFormatAndToUpperWord(unHyphenedWord);
                                            logger.Info("Inserting: " + unHyphenedUpperWord);
                                            if (hyphenedUpperWord.Length > 0)
                                            {
                                                InsertWordToChapterDict(unHyphenedUpperWord, fileNum, chapterJSON);
                                            }
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // This should never occur, BUT I've had this issue using Google's Vision API
                            logger.Warn("Skipping, current candidate line: " + hyphenedLine.Text + " somehow had more or less than 8 coordinates...");
                        }
                    }
                }
                else
                {
                    // This should never occur, BUT I've had this issue using Google's Vision API
                    logger.Warn("Skipping, current hyphened line: " + hyphenedLine.Text + " somehow had more or less than 8 coordinates...");
                }
            }
        }

        private void GenerateMasterJSON(string masterFile, MasterDictionary masterDictionary)
        {
            // Generate master dictionary JSON file
            logger.Info("Generating word dictionary.");
            File.WriteAllText(masterFile, JsonConvert.SerializeObject(masterDictionary, Formatting.Indented), System.Text.Encoding.UTF8);
        }

        private void InsertWordToChapterDict(string upperWord, int fileNum, ChapterJSON chapterJSON)
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
        }

        private void RegenerateJSON(string jsonFilePath, ChapterJSON chapterJSON)
        {
            // Clear existing dictionary
            chapterJSON.MentionedWordChapterLocation = new Dictionary<string, List<int>>();

            // Regenerate...
            logger.Info(string.Format("Regenerating: {0}...", jsonFilePath));
            foreach (var page in chapterJSON.Pages)
            {
                foreach (var words in page.Words)
                {
                    if (words.Text.EndsWith('-') || words.Text.StartsWith('-'))
                    {
                        FixHyphens(chapterJSON, words, page.Page);
                    }

                    var listOfWords = words.Text.Split(' ');
                    foreach (var word in listOfWords)
                    {
                        var upperWord = RegexFormatAndToUpperWord(word);
                        if (upperWord.Length > 0)
                        {
                            InsertWordToChapterDict(upperWord, page.Page, chapterJSON);
                        }
                    }
                }
            }

            // Rewrite!
            logger.Info(string.Format("Overwriting {0}...", jsonFilePath));
            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(chapterJSON, Formatting.Indented), System.Text.Encoding.UTF8); // WILL OVERWRITE!
        }

        private string RegexFormatAndToUpperWord(string originalWord)
        {
            var regexedWord = Regex.Replace(originalWord, @"([.,;!?""]|[-'#:]$)", "");
            regexedWord = Regex.Replace(regexedWord, @"([.,;!?""]|[-'#:]$)", "");
            var upperWord = regexedWord.ToUpper();

            return upperWord;
        }

        private string TrimAllHyphens(string originalWord)
        {
            var regexedWord = Regex.Replace(originalWord, @"[-]", "");
            return regexedWord;
        }

        private void RemoveChapterFromMaster(MasterDictionary masterDictionary, double chapterNum)
        {
            string chapterNumString = chapterNum.ToString().Replace(".", "-");
            foreach (var masterDictEntry in masterDictionary.MentionedWordLocation)
            {
                foreach (var wordEntry in masterDictEntry.Value)
                {
                    if (wordEntry.Key.Equals(chapterNumString))
                    {
                        masterDictionary.MentionedWordLocation[masterDictEntry.Key].Remove(wordEntry.Key);
                    }
                }
            }
        }
    }
}