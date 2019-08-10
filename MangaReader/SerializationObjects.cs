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

        public override bool Equals(object obj)
        {
            var other = obj as Coords;
            return (other.X == X && other.Y == Y);
        }

        public static bool operator ==(Coords a, Coords b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Coords a, Coords b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
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

    internal class Line
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
        public List<Line> Lines { get; set; }
        public int Width { get; set; }
    }

    internal class Words
    {
        public List<int> BoundingBox { get; set; }
        public string Text { get; set; }
    }
}