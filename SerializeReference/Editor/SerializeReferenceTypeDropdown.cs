using System;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodexFramework.SerializeReferenceDrawing.Editor
{
    internal sealed class SerializeReferenceTypeDropdown : AdvancedDropdown
    {
        private readonly Type[] _types;
        private readonly Action<Type> _onSelected;

        public SerializeReferenceTypeDropdown(AdvancedDropdownState state, Type[] types, Action<Type> onSelected)
            : base(state)
        {
            _types = types ?? Array.Empty<Type>();
            _onSelected = onSelected;
            minimumSize = new Vector2(260f, 300f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Types");
            root.AddChild(new Item("None", null));

            for (var i = 0; i < _types.Length; i++)
            {
                var type = _types[i];
                root.AddChild(new Item(type.Name, type));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is Item typed)
                _onSelected?.Invoke(typed.Type);
        }

        private sealed class Item : AdvancedDropdownItem
        {
            public Type Type { get; }

            public Item(string name, Type type) : base(name)
            {
                Type = type;
            }
        }
    }
}
