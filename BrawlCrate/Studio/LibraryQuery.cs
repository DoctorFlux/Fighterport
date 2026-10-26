using System;
using System.Linq;

namespace BrawlCrate.Studio
{
    public class LibraryQuery
    {
        public string Text { get; set; }
        public string Game { get; set; }
        public string Build { get; set; }
        public string Character { get; set; }
        public string ModelId { get; set; }
        public string GameSourceId { get; set; }
        public string GameBuildText { get; set; }
        public string CharacterModelText { get; set; }
        public LibrarySourceState? Availability { get; set; }
        public string Channel { get; set; }
        public bool FavoritesOnly { get; set; }
        public bool HasFilters => FavoritesOnly || Availability.HasValue || new[] { Text, Game, Build, Character, ModelId, GameSourceId, GameBuildText, CharacterModelText, Channel }.Any(s => !string.IsNullOrWhiteSpace(s));
        internal LibraryQuery Snapshot()
        {
            var copy = (LibraryQuery)MemberwiseClone();
            if (new[] { Text, Game, Build, Character, ModelId, GameSourceId, GameBuildText, CharacterModelText, Channel }.Any(s => s != null && s.Length > 512) ||
                Availability.HasValue && !Enum.IsDefined(typeof(LibrarySourceState), Availability.Value))
                throw new ArgumentException("Invalid library filter.");
            return copy;
        }
        internal static string[] Words(string query) => (query ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        internal static bool WordsMatch(string text, string[] words) => words.All(word => (text ?? "").IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        internal static bool Exact(string actual, string filter) => string.IsNullOrEmpty(filter) || string.Equals(actual, filter, StringComparison.Ordinal);
    }
}
