using System;
using System.Collections.Generic;
using Tripwire.Core;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>Searchable picker over every callable Udon API member, grouped by namespace and type.</summary>
    internal sealed class UdonApiDropdown : AdvancedDropdown
    {
        readonly Action<CallSpec> onPick;

        sealed class Item : AdvancedDropdownItem
        {
            public readonly CallSpec Spec;
            public Item(string name, CallSpec spec) : base(name) { Spec = spec; }
        }

        public UdonApiDropdown(AdvancedDropdownState state, Action<CallSpec> onPick) : base(state)
        {
            this.onPick = onPick;
            minimumSize = new Vector2(360, 480);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Udon API");
            var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            foreach (var c in UdonApi.All)
            {
                var parent = root;
                var path = "";
                foreach (var segment in c.DeclaringType.Split('.'))
                {
                    path += "." + segment;
                    AdvancedDropdownItem group;
                    if (!groups.TryGetValue(path, out group))
                    {
                        group = new AdvancedDropdownItem(segment);
                        parent.AddChild(group);
                        groups.Add(path, group);
                    }
                    parent = group;
                }
                parent.AddChild(new Item(c.Display(), c));
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            var picked = item as Item;
            if (picked != null) onPick(picked.Spec);
        }
    }
}
