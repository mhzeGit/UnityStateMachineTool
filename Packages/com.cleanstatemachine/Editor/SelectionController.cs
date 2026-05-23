using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class SelectionController
    {
        private readonly List<ISelectable> _selected = new();

        public IReadOnlyList<ISelectable> Selected => _selected;
        public int Count => _selected.Count;

        public event Action SelectionChanged;

        public bool IsSelected(ISelectable item)
        {
            return _selected.Contains(item);
        }

        public void Select(ISelectable item)
        {
            if (IsSelected(item))
                return;

            item.IsSelected = true;
            _selected.Add(item);
            SelectionChanged?.Invoke();
        }

        public void Deselect(ISelectable item)
        {
            if (!_selected.Remove(item))
                return;

            item.IsSelected = false;
            SelectionChanged?.Invoke();
        }

        public void Toggle(ISelectable item)
        {
            if (IsSelected(item))
                Deselect(item);
            else
                Select(item);
        }

        public void SelectOnly(ISelectable item)
        {
            Clear();
            Select(item);
        }

        public void SelectRange(IEnumerable<ISelectable> items)
        {
            foreach (var item in items)
            {
                if (!IsSelected(item))
                {
                    item.IsSelected = true;
                    _selected.Add(item);
                }
            }
            SelectionChanged?.Invoke();
        }

        public void Clear()
        {
            if (_selected.Count == 0)
                return;

            for (int i = 0; i < _selected.Count; i++)
                _selected[i].IsSelected = false;

            _selected.Clear();
            SelectionChanged?.Invoke();
        }
    }
}
