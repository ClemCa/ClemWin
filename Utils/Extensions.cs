namespace ClemWin;

public static class Extensions
{
    public static bool Contains(this string source, string searchText, int startIndex, StringComparison comparison, out int index)
    {
        index = source.IndexOf(searchText, startIndex, comparison);
        return index >= 0;
    }
    public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
            yield return item;
        }
    }
}
