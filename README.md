# KaguyaOCR

**Note that I am likely deprecating this tool for a rewritten one that is a bit less cobbled together and easier to use.**

A tool for reading in ~~Kaguya~~manga pages and generating a resulting OCR JSON file
for a chapter, in addition to a master dictionary, [using MS's Reader tool from their Cognitive Services](https://azure.microsoft.com/en-us/services/cognitive-services/computer-vision/).  In particular, it uses the Read API, not the OCR or Recognize Text API (see [here](https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/concept-recognizing-text) for the differences).

Note this is probably not the cleanest code or anything, and was kinda cobbled together to just work.  Improvements to usability and less jankiness will come in the future (I swear).  Furthermore, quality of the OCR is entirely dependant on MS's OCR.  This just formats the results into useful forms.

Despite the name and the fact that it was written for https://guya.moe/, this will work fine for any image/manga/comic.

### What it does

The main use case is to generate a JSON file containing all words found and their locations for a chapter (I'll refer to this as the "chapter" JSON) and a dictionary containing all words for that series and their respective locations, chapter and page wise (henceforth refered to as the "master" dictionary).

When fed a directory containing image files for a chapter, it'll output a chapter JSON (ie: chapter 1 gives ``1.json``) and a master dictionary JSON (defaults to ``master_dictionary.json`` in the same directory).  See below for an example of the JSON format.

### To use
To build from source, clone and open the solution, then Publish.  If you're using VS Studio, it should be fairly straightforward.  If not, you can publish from command line using dotnet:

```bash
dotnet publish MangaReader.sln
```

To just use the latest release, download from Releases.  Note that the most up-to-date version will always be from the repo, so cloning from that is probably preferable.

In both cases, you'll need to include a config.json file with the following structure in the same directory as the built program (where ``MangaReader.dll`` is located):
```json
{
    "subscriptionKey": "your-microsoft-cognitive-key",
    "personalEndpoint": "your-endpoint"
}
```

To run the program, you will need .NET Core, as well as an internet connection (or if you're running Cognitive off a Docker container, I suppose not).

Then, open a prompt in the release directory.  To see a list of flags:
```bash
dotnet MangaReader.dll --help
```

To OCR a folder (representing a chapter), run the following:
```bash
dotnet MangaReader.dll -i "your/input/directory/folder" -c chapter_number -o "optional/output/json/file/path.json" -m "optional/output/master/dictionary/path.json"
```

Note that your files in the directory **must** be in order, namewise!  That is, page 1 should be the first file, page 2 should be the second file, etc.  To be safe, just pad your filenames with 0's if needed (01, 02, 03... 15, for example).

To just add a chapter JSON file to a master dictionary (or to just create a master dictionary from a chapter JSON file):
```bash
dotnet MangaReader.dll -i "your/input/json/file.json" -m "optional/output/master/dictionary/path.json"
```

To add **multiple** chapter JSON files in a directory to a master dictionary:
```bash
dotnet MangaReader.dll -b "chapter/json/directory/path" -m "optional/output/master/dictionary/path"
```

To add verbosity or logging (logs to ``diagnostics.log`` in the program directory), use ``-v`` or ``-l`` tags respectively.

### JSON format

Two JSON files are generated at the end (at most).  A chapter JSON file and a master dictionary JSON.

The chapter JSON file has the following structure:
```json
{
  "Chapter": 1.0,
  "Pages": [
    {
      "Words": [
        {
          "BoundingBox": [
            {
              "X": 156,
              "Y": 1196
            },
            {
              "X": 455,
              "Y": 1194
            },
            {
              "X": 456,
              "Y": 1225
            },
            {
              "X": 157,
              "Y": 1227
            }
          ],
          "Text": "BOOK DESIGN IN TSUKANO HIROTAKA"
        }
      ],
      "Page": 1,
      "Height": 1250,
      "Width": 3095
        }
  ],
  "MentionedWordChapterLocation": {
    "CANDIDACY": [
      30
    ],
    "HIS": [
      13,
      16,
      17,
      23,
      25
    ]
  }
}
```
(note this is a heavily truncated result).  The bounding box coordinates go top left, top right, bottom right, bottom left, in that order.  Each bounding box represents a *phrase* or a group of words that MS Reader detected.

The master dictionary has a slightly similar format to the ``MentionedWordChapterLocation`` field, but includes a reference to the chapter:
```json
{
    "MentionedWordLocation": {
      "KAGUYA": {
          "1": [
            11,
            12,
            13
          ],
          "10": [
            5,
            11,
            15,
            17,
            20
          ],
          "100": [
            1,
            3,
            4,
            5
          ],
          "101": [
            6,
            7,
            11,
            13,
            15,
            18
          ],
          "101-1": [
            7
          ]
        }
    }
}
```
where each word maps to a (chapter, page array) dictionary.

### Contributions

Well, this was originally a private repo, but if anyone wants to contribute, feel free to.
