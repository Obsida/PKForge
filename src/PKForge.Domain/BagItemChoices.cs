namespace PKForge.Domain;

/// <summary>Only named, legal items absent from the current bag belong in the add picker.</summary>
public static class BagItemChoices
{
    public static IReadOnlyList<int> AvailableToAdd(IEnumerable<BagPouch> bag, IEnumerable<int> legalIds, IReadOnlyList<string> names)
    {
        var owned = bag.SelectMany(pouch => pouch.Items).Where(item => item.Count > 0).Select(item => item.Id).ToHashSet();
        return legalIds.Where(id => id > 0 && id < names.Count && !string.IsNullOrWhiteSpace(names[id]) && !owned.Contains(id))
            .Distinct().ToArray();
    }
}
