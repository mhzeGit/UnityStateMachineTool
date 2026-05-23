using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class CommentGroupView : VisualElement, ISelectable
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                UpdateBorderStyle();
            }
        }

        private Rect _rect;
        private readonly List<StateView> _members = new();

        private string _labelText;
        public string Label
        {
            get => _labelText;
            set
            {
                _labelText = value;
                if (_label != null)
                    _label.text = value;
            }
        }
        public IReadOnlyList<StateView> Members => _members;

        public bool RemoveMember(StateView state)
        {
            return _members.Remove(state);
        }

        public void AddMember(StateView state)
        {
            if (!_members.Contains(state) && !state.IsEntry)
                _members.Add(state);
        }

        public event Action<CommentGroupView, string, string> EditingCommitted;

        private const float PadH = 20f;
        private const float PadTop = 30f;
        private const float PadBot = 15f;
        private const float CRadius = 12f;

        private static readonly Color DefaultGroupColor = new Color(0.18f, 0.18f, 0.18f, 0.35f);
        private static readonly Color BorderCol = new Color(0.40f, 0.40f, 0.40f, 0.35f);
        private static readonly Color SelBorderCol = new Color(0.70f, 0.70f, 0.70f, 0.80f);

        private Color _groupColor;
        public Color GroupColor
        {
            get => _groupColor;
            set
            {
                if (_groupColor == value) return;
                _groupColor = value;
                UpdateGroupColors();
            }
        }

        private readonly VisualElement _header;
        private readonly Label _label;
        private readonly TextField _editField;

        public bool IsEditing { get; private set; }
        public string EditingBuffer { get; private set; }

        private float _lastZoom = 1f;

        public CommentGroupView(IEnumerable<StateView> members, string label = "Comment Group")
        {
            Label = label;
            _groupColor = DefaultGroupColor;
            _members.AddRange(members);

            if (_members.Count > 0)
            {
                Rect b = GetMembersBounds();
                _rect = new Rect(b.x - PadH, b.y - PadTop, b.width + PadH * 2f, b.height + PadTop + PadBot);
            }
            else
            {
                _rect = new Rect(0f, 0f, 160f, 40f);
            }

            pickingMode = PickingMode.Ignore;
            style.position = UnityEngine.UIElements.Position.Absolute;
            style.overflow = Overflow.Hidden;

            _header = new VisualElement();
            _header.pickingMode = PickingMode.Ignore;
            _header.AddToClassList("comment-group__header");
            Add(_header);

            _label = new Label(Label);
            _label.pickingMode = PickingMode.Ignore;
            _label.AddToClassList("comment-group__label");
            _header.Add(_label);

            _editField = new TextField();
            _editField.AddToClassList("comment-group__edit-field");
            _editField.style.display = DisplayStyle.None;
            _editField.RegisterCallback<KeyDownEvent>(OnEditFieldKeyDown);
            _editField.RegisterCallback<FocusOutEvent>(OnEditFieldFocusOut);
            _header.Add(_editField);

            UpdateGroupColors();
            UpdateBorderStyle();
        }

        private void UpdateGroupColors()
        {
            style.backgroundColor = _groupColor;
            Color headerColor = new Color(_groupColor.r, _groupColor.g, _groupColor.b, Mathf.Min(1f, _groupColor.a + 0.20f));
            _header.style.backgroundColor = headerColor;
        }

        private Rect GetMembersBounds()
        {
            if (_members.Count == 0)
                return new Rect(0f, 0f, 160f, 40f);

            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < _members.Count; i++)
            {
                Rect r = _members[i].GetGraphBounds();
                if (r.xMin < xMin) xMin = r.xMin;
                if (r.xMax > xMax) xMax = r.xMax;
                if (r.yMin < yMin) yMin = r.yMin;
                if (r.yMax > yMax) yMax = r.yMax;
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public Vector2 Position
        {
            get => _rect.position;
            set
            {
                Vector2 delta = value - _rect.position;
                if (delta.sqrMagnitude < 0.0001f) return;

                for (int i = 0; i < _members.Count; i++)
                {
                    if (!_members[i].IsEntry)
                        _members[i].Position += delta;
                }

                _rect.position = value;
            }
        }

        public Vector2 Size
        {
            get => _rect.size;
            set => _rect.size = value;
        }

        public Rect GetGraphBounds() => _rect;

        public void SetRect(Rect rect)
        {
            _rect = rect;
        }

        public void SyncContainedStates(IEnumerable<StateView> allStates)
        {
            foreach (var state in allStates)
            {
                if (state.IsEntry) continue;

                Rect stateRect = state.GetGraphBounds();
                bool contained = stateRect.xMin >= _rect.xMin - 0.001f &&
                                 stateRect.yMin >= _rect.yMin - 0.001f &&
                                 stateRect.xMax <= _rect.xMax + 0.001f &&
                                 stateRect.yMax <= _rect.yMax + 0.001f;

                if (contained && !_members.Contains(state))
                    _members.Add(state);
                else if (!contained && _members.Remove(state))
                {
                    // state was removed from group
                }
            }
        }

        public new bool ContainsPoint(Vector2 p)
        {
            Rect b = GetGraphBounds();
            if (!b.Contains(p)) return false;

            float r = CRadius;
            if (p.x < b.x + r && p.y < b.y + r) { float dx = p.x - (b.x + r); float dy = p.y - (b.y + r); return dx * dx + dy * dy <= r * r; }
            if (p.x > b.xMax - r && p.y < b.y + r) { float dx = p.x - (b.xMax - r); float dy = p.y - (b.y + r); return dx * dx + dy * dy <= r * r; }
            if (p.x < b.x + r && p.y > b.yMax - r) { float dx = p.x - (b.x + r); float dy = p.y - (b.yMax - r); return dx * dx + dy * dy <= r * r; }
            if (p.x > b.xMax - r && p.y > b.yMax - r) { float dx = p.x - (b.xMax - r); float dy = p.y - (b.yMax - r); return dx * dx + dy * dy <= r * r; }
            return true;
        }

        public void UpdateScreenPosition(float zoom, Vector2 panOffset)
        {
            _lastZoom = zoom;

            Rect b = GetGraphBounds();
            Vector2 sp = b.position * zoom + panOffset;
            Vector2 ss = b.size * zoom;

            style.left = sp.x;
            style.top = sp.y;
            style.width = ss.x;
            style.height = ss.y;

            int r = Mathf.Max(1, Mathf.RoundToInt(CRadius * zoom));
            style.borderTopLeftRadius = r;
            style.borderTopRightRadius = r;
            style.borderBottomLeftRadius = r;
            style.borderBottomRightRadius = r;

            UpdateBorderStyle();

            float headerH = Mathf.Max(1f, 24f * zoom);
            _header.style.height = headerH;
            _label.style.fontSize = Mathf.RoundToInt(11f * zoom);

            if (IsEditing)
                _editField.style.fontSize = Mathf.RoundToInt(11f * zoom);
        }

        private void UpdateBorderStyle()
        {
            int bw = Mathf.Max(1, Mathf.RoundToInt((_isSelected ? 2f : 1f) * _lastZoom));
            style.borderLeftWidth = bw;
            style.borderRightWidth = bw;
            style.borderTopWidth = bw;
            style.borderBottomWidth = bw;

            Color bc = _isSelected ? SelBorderCol : BorderCol;
            style.borderLeftColor = bc;
            style.borderRightColor = bc;
            style.borderTopColor = bc;
            style.borderBottomColor = bc;
        }

        public void DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
        }

        // ─── Inline Rename (matching BlackboardPanel pattern) ──────────

        public void StartEditing()
        {
            if (IsEditing) return;

            IsEditing = true;
            EditingBuffer = Label;

            _label.style.display = DisplayStyle.None;
            _editField.value = Label;
            _editField.style.display = DisplayStyle.Flex;

            schedule.Execute(() =>
            {
                _editField.Focus();
                _editField.SelectAll();
            }).StartingIn(0);
        }

        public void CommitEditing()
        {
            if (!IsEditing) return;

            string newName = _editField.value;
            string oldName = EditingBuffer;

            IsEditing = false;

            _label.style.display = DisplayStyle.Flex;
            _editField.style.display = DisplayStyle.None;

            if (newName != oldName && !string.IsNullOrEmpty(newName))
            {
                Label = newName;
                _label.text = newName;
                EditingCommitted?.Invoke(this, oldName, newName);
            }
            else
            {
                Label = oldName;
                _label.text = oldName;
                EditingCommitted?.Invoke(this, oldName, oldName);
            }
        }

        public void CancelEditing()
        {
            if (!IsEditing) return;

            IsEditing = false;
            Label = EditingBuffer;
            _label.text = EditingBuffer;

            _label.style.display = DisplayStyle.Flex;
            _editField.style.display = DisplayStyle.None;

            EditingCommitted?.Invoke(this, EditingBuffer, EditingBuffer);
        }

        private void OnEditFieldKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                CommitEditing();
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                CancelEditing();
                e.StopPropagation();
            }
        }

        private void OnEditFieldFocusOut(FocusOutEvent e)
        {
            if (IsEditing)
                CommitEditing();
        }
    }
}
