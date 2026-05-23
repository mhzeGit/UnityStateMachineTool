using UnityEngine;

namespace CleanStateMachine
{
    public interface IContextMenuProvider
    {
        void AddItemsToMenu(MenuDropdown.IBuilder menu, Vector2 graphMousePosition);
    }
}
