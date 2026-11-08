namespace Commerce.Domain.Catalog;

public static class CombinationKey
{
    public static string Build(IEnumerable<Guid> optionValueIds)
    {
        var ordered = optionValueIds.OrderBy(id => id).Select(id => id.ToString("N")).ToArray();
        return string.Join('.', ordered);
    }
}
