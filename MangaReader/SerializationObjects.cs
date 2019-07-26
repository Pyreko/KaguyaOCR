using System.Collections.Generic;

namespace MangaReader
{
    internal class ChapterJSON
    {
        public double Chapter { get; set; }
        public Dictionary<string, List<int>> MentionedWordChapterLocation { get; set; }
        public List<FormattedJSONPage> Pages { get; set; }
    }

    internal class Coords
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    internal class FormattedJSONPage
    {
        public int Height { get; set; }
        public int Page { get; set; }
        public int Width { get; set; }
        public List<FormattedWord> Words { get; set; }
    }

    internal class FormattedWord
    {
        public List<Coords> BoundingBox { get; set; }
        public string Text { get; set; }
    }

    internal class Lines
    {
        public List<int> BoundingBox { get; set; }
        public string Text { get; set; }
        public List<Words> Words { get; set; }
    }

    internal class MasterDictionary
    {
        public SortedDictionary<string, SortedDictionary<string, List<int>>> MentionedWordLocation { get; set; }
    }

    internal class ResponseJSON
    {
        public int Height { get; set; }
        public List<Lines> Lines { get; set; }
        public int Width { get; set; }
    }

    internal class Words
    {
        public List<int> BoundingBox { get; set; }
        public string Text { get; set; }
    }
}