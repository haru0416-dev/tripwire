using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>A searchable list of choices ("Group/Name" paths become groups), for popups too long to scan.</summary>
    internal sealed class ChoiceDropdown : AdvancedDropdown
    {
        readonly string title;
        readonly string[] choices;
        readonly Action<int> onPick;

        sealed class Item : AdvancedDropdownItem
        {
            public readonly int Index;
            public Item(string name, int index) : base(name) { Index = index; }
        }

        public ChoiceDropdown(string title, string[] choices, Action<int> onPick) : base(new AdvancedDropdownState())
        {
            this.title = title;
            this.choices = choices;
            this.onPick = onPick;
            minimumSize = new Vector2(280, 360);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(title);
            var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            for (int i = 0; i < choices.Length; i++)
            {
                var parts = choices[i].Split('/');
                var parent = root;
                for (int k = 0; k < parts.Length - 1; k++)
                {
                    var key = string.Join("/", parts, 0, k + 1);
                    if (!groups.TryGetValue(key, out var group)) { groups[key] = group = new AdvancedDropdownItem(parts[k]); parent.AddChild(group); }
                    parent = group;
                }
                parent.AddChild(new Item(parts[parts.Length - 1], i));
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is Item choice) onPick(choice.Index);
        }

        static readonly Dictionary<int, int> picks = new Dictionary<int, int>();

        /// <summary>
        /// Like EditorGUILayout.Popup, but the list opens with a search field. The pick arrives on a later pass (the
        /// list is its own window), with GUI.changed set as a popup's would be.
        /// </summary>
        public static int Layout(string label, int index, string[] choices, EditorWindow owner)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (picks.TryGetValue(id, out var picked)) { picks.Remove(id); index = picked; GUI.changed = true; }
            var r = EditorGUILayout.GetControlRect();
            if (label != null) r = EditorGUI.PrefixLabel(r, new GUIContent(label));
            var shown = index >= 0 && index < choices.Length ? choices[index].Replace("/", " › ") : "";
            if (GUI.Button(r, shown, EditorStyles.popup))
                new ChoiceDropdown(label ?? "", choices, i => { picks[id] = i; owner?.Repaint(); }).Show(r);
            return index;
        }
    }
}
