using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace ClickableTerminal
{
    // Saved by item name, so they still work when mods change the store's order.
    internal static class SavedCarts
    {
        internal class Saved
        {
            internal string Name;
            internal readonly List<KeyValuePair<string, int>> Lines = new List<KeyValuePair<string, int>>();

            internal int Count => Lines.Sum(line => line.Value);
        }

        private static List<Saved> all;

        private static string FilePath => Path.Combine(Paths.ConfigPath, "dev.davesco.ClickableTerminal.carts.txt");

        internal static List<Saved> All
        {
            get
            {
                if (all == null)
                {
                    Read();
                }

                return all;
            }
        }

        internal static void Save(Terminal terminal, string name)
        {
            var items = terminal.buyableItemsList;
            var saved = new Saved { Name = name.Trim() };
            foreach (var line in Cart.Lines)
            {
                if (items != null && line.Key < items.Length && items[line.Key] != null)
                {
                    saved.Lines.Add(new KeyValuePair<string, int>(items[line.Key].itemName, line.Value));
                }
            }

            All.RemoveAll(each => string.Equals(each.Name, saved.Name, StringComparison.OrdinalIgnoreCase));
            All.Add(saved);
            Write();
        }

        internal static void Delete(Saved saved)
        {
            All.Remove(saved);
            Write();
        }

        internal static int Load(Terminal terminal, Saved saved)
        {
            var items = terminal.buyableItemsList;
            var left = 0;
            foreach (var line in saved.Lines)
            {
                var index = items == null ? -1 : Array.FindIndex(items, item => item != null && item.itemName == line.Key);
                for (var i = 0; i < line.Value; i++)
                {
                    if (index < 0 || !Cart.Add(terminal, index))
                    {
                        left++;
                    }
                }
            }

            return left;
        }

        internal static int Total(Terminal terminal, Saved saved)
        {
            var items = terminal.buyableItemsList;
            var total = 0;
            foreach (var line in saved.Lines)
            {
                var index = items == null ? -1 : Array.FindIndex(items, item => item != null && item.itemName == line.Key);
                if (index >= 0)
                {
                    total += Cart.PriceOf(terminal, index) * line.Value;
                }
            }

            return total;
        }

        // "name|item=amount;item=amount"
        private static void Read()
        {
            all = new List<Saved>();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                foreach (var row in File.ReadAllLines(FilePath))
                {
                    var parts = row.Split('|');
                    if (parts.Length != 2 || parts[0].Trim().Length == 0)
                    {
                        continue;
                    }

                    var saved = new Saved { Name = parts[0].Trim() };
                    foreach (var pair in parts[1].Split(';'))
                    {
                        var cut = pair.LastIndexOf('=');
                        if (cut > 0 && int.TryParse(pair.Substring(cut + 1), out var amount) && amount > 0)
                        {
                            saved.Lines.Add(new KeyValuePair<string, int>(pair.Substring(0, cut), amount));
                        }
                    }

                    all.Add(saved);
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Saved carts could not be read: " + e.Message);
            }
        }

        private static void Write()
        {
            try
            {
                File.WriteAllLines(FilePath, All.Select(saved =>
                    saved.Name.Replace("|", " ") + "|" + string.Join(";", saved.Lines.Select(line => line.Key + "=" + line.Value))));
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Saved carts could not be written: " + e.Message);
            }
        }
    }
}
