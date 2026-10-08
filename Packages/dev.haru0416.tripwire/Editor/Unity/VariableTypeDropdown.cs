using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>Searchable picker over every type a variable can have (the types Udon can hold), grouped by namespace.</summary>
    internal sealed class VariableTypeDropdown : AdvancedDropdown
    {
        readonly Action<Type> onPick;

        sealed class Item : AdvancedDropdownItem
        {
            public readonly Type Type;
            public Item(string name, Type type) : base(name) { Type = type; }
        }

        public VariableTypeDropdown(AdvancedDropdownState state, Action<Type> onPick) : base(state)
        {
            this.onPick = onPick;
            minimumSize = new Vector2(320, 420);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Variable type");
            var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            foreach (var type in UdonApi.VariableTypes)
            {
                var csName = UdonApi.CSharpName(type);
                var element = type.IsArray ? type.GetElementType() : type;
                var ns = element.Namespace ?? "(global)";
                AdvancedDropdownItem group;
                if (!groups.TryGetValue(ns, out group))
                {
                    group = new AdvancedDropdownItem(ns);
                    root.AddChild(group);
                    groups.Add(ns, group);
                }
                group.AddChild(new Item(csName.Substring(csName.LastIndexOf('.', csName.Length - 1 - (type.IsArray ? 2 : 0)) + 1), type));
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            var picked = item as Item;
            if (picked != null) onPick(picked.Type);
        }
    }
}
