
using PKForge.Engine;
using PKHeX.Core;
using System.Reflection;
using PKForge.Domain;
using Xunit;

namespace PKForge.Engine.Tests;

public sealed class AddItemProbe
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public void AddingHidesAnItemUntilItIsRemoved(int generation)
    {
        using var session = new SaveEngine().OpenBlankSession(generation);
        var pouch = session.GetBag().First();
        var names = session.GetItemNames();
        var legal = session.GetPouchLegalItems(pouch.Name);
        var initial = BagItemChoices.AvailableToAdd(session.GetBag(), legal, names);
        var item = initial.First();
        session.SetItemCount(pouch.Name, item, 5);
        Assert.DoesNotContain(item, BagItemChoices.AvailableToAdd(session.GetBag(), legal, names));
        session.SetItemCount(pouch.Name, item, 0);
        Assert.Contains(item, BagItemChoices.AvailableToAdd(session.GetBag(), legal, names));
    }

    [Fact]
    public void AddPickerIgnoresInvalidIdsAndChecksEveryPouch()
    {
        BagPouch[] bag = [new("Items", [new(1, 5)]), new("Medicine", [new(2, 1), new(3, 0)])];
        string[] names = ["None", "Owned", "Owned elsewhere", "Removed", " ", "New"];
        Assert.Equal([3, 5], BagItemChoices.AvailableToAdd(bag, [-1, 0, 1, 2, 3, 4, 5, 5, 999], names));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public void EveryPouchOffersAddableItems(int generation)
    {
        var engine = new SaveEngine();
        using var session = engine.OpenBlankSession(generation);
        var names = session.GetItemNames();
        foreach (var pouch in session.GetBag())
        {
            var legal = session.GetPouchLegalItems(pouch.Name);
            var named = legal.Where(id => id < names.Count && names[id].Length > 0).ToList();
            Assert.True(named.Count > 0, $"gen{generation} pouch {pouch.Name}: {legal.Count} legal, {named.Count} named");
        }
    }

    [Fact]
    public void ItemCountIsClampedToTheGamesRealLimit()
    {
        var engine = new SaveEngine();
        using var session = engine.OpenBlankSession(1);
        var pouch = session.GetBag().First();
        var item = session.GetPouchLegalItems(pouch.Name).First(id => id > 0);

        var stored = session.SetItemCount(pouch.Name, item, 999);

        Assert.Equal(99, stored);
        Assert.Equal(stored, session.GetBag().Single(p => p.Name == pouch.Name).Items.Single(i => i.Id == item).Count);
    }

    [Fact]
    public void Gen4AddingItemCompactsZeroCountHolesForTheGame()
    {
        using var session = (SaveEngineSession)new SaveEngine().OpenBlankSession(4);
        var saveField = typeof(SaveEngineSession).GetField("_save", BindingFlags.Instance | BindingFlags.NonPublic);
        var save = Assert.IsAssignableFrom<SaveFile>(saveField?.GetValue(session));
        var bag = save.Inventory;
        var medicine = bag.Pouches.Single(p => p.Type == InventoryType.Medicine);

        medicine.Items[0] = medicine.GetEmpty(17, 1); // Potion
        medicine.Items[1] = medicine.GetEmpty(1, 0);  // stale zero-count entry / hole
        medicine.Items[2] = medicine.GetEmpty(18, 2); // Antidote beyond the hole
        bag.CopyTo(save);

        Assert.Equal(50, session.SetItemCount("Medicine", 50, 50)); // Rare Candy

        medicine = save.Inventory.Pouches.Single(p => p.Type == InventoryType.Medicine);
        var occupied = medicine.Items.TakeWhile(item => item.Index != 0 && item.Count > 0).ToArray();
        Assert.Equal(3, occupied.Length);
        Assert.Contains(occupied, item => item.Index == 17 && item.Count == 1);
        Assert.Contains(occupied, item => item.Index == 18 && item.Count == 2);
        Assert.Contains(occupied, item => item.Index == 50 && item.Count == 50);
        Assert.All(medicine.Items.Skip(occupied.Length), item => Assert.True(item.Index == 0 || item.Count == 0));
    }
}
