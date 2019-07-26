# KaguyaOCR

A tool for reading in ~~Kaguya~~manga pages and generating a resulting OCR JSON file
for a chapter, in addition to a master dictionary, using MS's Reader tool from their Cognitive Services.

## To use
To build from source, clone and open the solution, then publish.  To just use, download from Releases.

In both cases, you'll need to include a config.json file with the following structure in the same directory as the built program:
```json
{
    "subscriptionKey": "your-microsoft-cognitive-key",
    "personalEndpoint": "your-endpoint"
}
```

To run the program, you will need .NET Core, as well as an internet connection.

Then, open a prompt in the release directory.  To see a list of flags:
```bash
dotnet MangaReader.dll --help
```

To OCR a folder (representing a chapter), run the following:
```bash
dotnet MangaReader.dll -i "your/input/directory/folder" -c chapter_number -o "optional/output/json/file/path.json" -m "optional/output/master/dictionary/path.json"
```

To just add a chapter JSON file to a master dictionary (or to just create a master dictionary from a chapter JSON file):
```bash
dotnet MangaReader.dll -i "your/input/json/file.json" -m "optional/output/master/dictionary/path.json"
```

To add **multiple** chapter JSON files in a directory to a master dictionary:
```bash
dotnet MangaReader.dll -b "chapter/json/directory/path" -m "optional/output/master/dictionary/path"
```

To add verbosity or logging (logs to ``diagnostics.log`` in the program directory), use ``-v`` or ``--l`` tags respectively.

## JSON

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
