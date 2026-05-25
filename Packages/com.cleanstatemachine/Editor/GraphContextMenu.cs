using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class GraphContextMenu
    {
        private readonly List<IContextMenuProvider> _providers = new();

        private StateView _contextNode;
        private CommentGroupView _contextGroup;

        public event Action<Vector2> CreateStateRequested;
        public event Action<Vector2> CreateSubStateMachineRequested;
        public event Action<Vector2> CreateExternalReferenceRequested;
        public event Action<StateView> ConnectRequested;
        public event Action<CommentGroupView> UngroupRequested;
        public event Action CopyRequested;
        public event Action PasteRequested;
        public event Action DeleteRequested;
        public event Action<StateView> ToggleBreakpointRequested;

        public void AddProvider(IContextMenuProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (!_providers.Contains(provider))
                _providers.Add(provider);
        }

        public void RemoveProvider(IContextMenuProvider provider)
        {
            _providers.Remove(provider);
        }

        public void Show(VisualElement root, Vector2 screenPosition, Vector2 graphMousePosition, StateView contextNode = null, CommentGroupView contextGroup = null, bool hasSelection = false, bool hasClipboard = false, bool contextNodeHasBreakpoint = false)
        {
            _contextNode = contextNode;
            _contextGroup = contextGroup;

            MenuDropdown.Show(root, screenPosition, menu =>
            {
                AddDefaultItems(menu, graphMousePosition, hasSelection, hasClipboard, contextNodeHasBreakpoint);
                AddProviderItems(menu, graphMousePosition);
            });
        }

        private void AddDefaultItems(MenuDropdown.IBuilder menu, Vector2 graphMousePosition, bool hasSelection, bool hasClipboard, bool contextNodeHasBreakpoint = false)
        {
            menu.AddItem("Create State", () => CreateStateRequested?.Invoke(graphMousePosition));
            menu.AddItem("Create Sub State Machine", () => CreateSubStateMachineRequested?.Invoke(graphMousePosition));
            menu.AddItem("Create External Reference", () => CreateExternalReferenceRequested?.Invoke(graphMousePosition));

            if (_contextNode != null)
            {
                StateView captured = _contextNode;
                menu.AddItem("Connect", () => ConnectRequested?.Invoke(captured));

                if (contextNodeHasBreakpoint)
                    menu.AddItem("Remove Breakpoint", () => ToggleBreakpointRequested?.Invoke(captured));
                else
                    menu.AddItem("Add Breakpoint", () => ToggleBreakpointRequested?.Invoke(captured));
            }

            menu.AddSeparator();

            if (hasSelection)
                menu.AddItem("Copy", () => CopyRequested?.Invoke());
            else
                menu.AddDisabledItem("Copy");

            if (hasClipboard)
                menu.AddItem("Paste", () => PasteRequested?.Invoke());
            else
                menu.AddDisabledItem("Paste");

            if (hasSelection)
                menu.AddItem("Delete", new Color(0.85f, 0.2f, 0.2f), () => DeleteRequested?.Invoke());
            else
                menu.AddDisabledItem("Delete");

            if (_contextGroup != null)
            {
                menu.AddSeparator();
                CommentGroupView captured = _contextGroup;
                menu.AddItem("Ungroup", () => UngroupRequested?.Invoke(captured));
            }
        }

        private void AddProviderItems(MenuDropdown.IBuilder menu, Vector2 graphMousePosition)
        {
            for (int i = 0; i < _providers.Count; i++)
            {
                _providers[i].AddItemsToMenu(menu, graphMousePosition);
            }
        }
    }
}
