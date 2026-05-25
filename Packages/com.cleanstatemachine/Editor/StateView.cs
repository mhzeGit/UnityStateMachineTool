using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class StateView : VisualElement, ISelectable
    {
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                if (_nameLabel != null)
                    _nameLabel.text = value;
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (_fill != null)
                    _fill.EnableInClassList("state-view__fill--selected", value);
            }
        }

        public bool IsEntry { get; }
        private bool _isSubEntry;
        public bool IsSubEntry
        {
            get => _isSubEntry;
            set
            {
                _isSubEntry = value;
                UpdateSubStateMachineVisual();
            }
        }
        private bool _isSubStateMachine;
        public bool IsSubStateMachine
        {
            get => _isSubStateMachine;
            set
            {
                _isSubStateMachine = value;
                UpdateSubStateMachineVisual();
            }
        }
        public bool IsEditing { get; private set; }
        public string EditingBuffer { get; private set; }
        public MonoScript BehaviourScript { get; set; }
        public StateBehaviour BehaviourInstance { get; set; }
        public List<int> ChildIndices { get; set; } = new List<int>();

        private bool _isActive;
        private double _activatedAtTime;
        private double _deactivatedAtTime;
        private bool _wasBriefActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                if (value)
                    _activatedAtTime = Time.realtimeSinceStartup;
                else
                {
                    _wasBriefActive = (Time.realtimeSinceStartup - _activatedAtTime) < 0.2;
                    if (_wasBriefActive)
                        _deactivatedAtTime = Time.realtimeSinceStartup;
                }
                _isActive = value;
                if (_fill != null)
                {
                    if (value)
                        _fill.EnableInClassList("state-view__fill--active", true);
                    else if (!_wasBriefActive)
                        _fill.EnableInClassList("state-view__fill--active", false);
                }
            }
        }

        public int DataIndex { get; set; } = -1;

        public event Action<StateView, string, string> EditingCommitted; // state, oldName, newName

        private VisualElement _shadow;
        private VisualElement _glow;
        private VisualElement _fill;
        private Label _nameLabel;
        private TextField _editField;
        private VisualElement _editFieldInput;
        private VisualElement _subIcon;

        public const float DefaultWidth = 160f;
        public const float DefaultHeight = 40f;
        private const int BaseCornerRadius = 8;
        private const float PermanentBorderWidth = 1.5f;
        private const float GlowExpandPx = 6f;
        private const float GlowPulseSpeed = 2.5f;
        private const int GlowBlurKernel = 4;

        public StateView(Vector2 position, string name = "State", bool isEntry = false, bool isSubEntry = false)
        {
            Position = position;
            Size = new Vector2(DefaultWidth, DefaultHeight);
            _name = name;
            IsEntry = isEntry;
            _isSubEntry = isSubEntry;

            pickingMode = PickingMode.Ignore;
            style.position = UnityEngine.UIElements.Position.Absolute;
            style.overflow = Overflow.Visible;

            style.left = position.x;
            style.top = position.y;
            style.width = DefaultWidth;
            style.height = DefaultHeight;

            _shadow = new VisualElement();
            _shadow.AddToClassList("state-view__shadow");
            _shadow.pickingMode = PickingMode.Ignore;
            _shadow.style.position = UnityEngine.UIElements.Position.Absolute;
            Add(_shadow);

            _glow = new VisualElement();
            _glow.AddToClassList("state-view__glow");
            _glow.pickingMode = PickingMode.Ignore;
            _glow.style.position = UnityEngine.UIElements.Position.Absolute;
            Add(_glow);

            _fill = new VisualElement();
            _fill.AddToClassList("state-view__fill");
            _fill.pickingMode = PickingMode.Ignore;
            _fill.style.position = UnityEngine.UIElements.Position.Absolute;
            _fill.style.left = 0f;
            _fill.style.top = 0f;
            _fill.style.right = 0f;
            _fill.style.bottom = 0f;
            if (IsSubEntry)
                _fill.AddToClassList("state-view__fill--sub-entry");
            else if (IsEntry)
                _fill.AddToClassList("state-view__fill--entry");
            UpdateSubStateMachineVisual();
            Add(_fill);

            _nameLabel = new Label(_name);
            _nameLabel.AddToClassList("state-view__label");
            _nameLabel.pickingMode = PickingMode.Ignore;
            _nameLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _nameLabel.style.left = 0f;
            _nameLabel.style.top = 0f;
            _nameLabel.style.right = 0f;
            _nameLabel.style.bottom = 0f;
            Add(_nameLabel);

            _editField = new TextField();
            _editField.AddToClassList("state-view__edit-field");
            _editField.pickingMode = PickingMode.Ignore;
            _editField.style.position = UnityEngine.UIElements.Position.Absolute;
            _editField.style.left = 0f;
            _editField.style.top = 0f;
            _editField.style.right = 0f;
            _editField.style.bottom = 0f;
            _editField.style.display = DisplayStyle.None;
            _editField.RegisterCallback<KeyDownEvent>(OnEditFieldKeyDown);
            _editField.RegisterCallback<FocusOutEvent>(OnEditFieldFocusOut);
            Add(_editField);

            _editFieldInput = _editField.Q(className: "unity-base-text-field__input");

            _subIcon = new Label("\u2197");
            _subIcon.AddToClassList("state-view__sub-icon");
            _subIcon.pickingMode = PickingMode.Ignore;
            _subIcon.style.position = UnityEngine.UIElements.Position.Absolute;
            _subIcon.style.display = DisplayStyle.None;
            Add(_subIcon);

            InitializeGlowAnimation();
        }

        public Vector2 GetCenter()
        {
            return new Vector2(Position.x + Size.x * 0.5f, Position.y + Size.y * 0.5f);
        }

        public Rect GetGraphBounds()
        {
            return new Rect(Position.x, Position.y, Size.x, Size.y);
        }

        public new bool ContainsPoint(Vector2 graphPoint)
        {
            if (graphPoint.x < Position.x || graphPoint.x > Position.x + Size.x ||
                graphPoint.y < Position.y || graphPoint.y > Position.y + Size.y)
                return false;

            float r = BaseCornerRadius;

            if (graphPoint.x < Position.x + r && graphPoint.y < Position.y + r)
            {
                float dx = graphPoint.x - (Position.x + r);
                float dy = graphPoint.y - (Position.y + r);
                return dx * dx + dy * dy <= r * r;
            }

            if (graphPoint.x > Position.x + Size.x - r && graphPoint.y < Position.y + r)
            {
                float dx = graphPoint.x - (Position.x + Size.x - r);
                float dy = graphPoint.y - (Position.y + r);
                return dx * dx + dy * dy <= r * r;
            }

            if (graphPoint.x < Position.x + r && graphPoint.y > Position.y + Size.y - r)
            {
                float dx = graphPoint.x - (Position.x + r);
                float dy = graphPoint.y - (Position.y + Size.y - r);
                return dx * dx + dy * dy <= r * r;
            }

            if (graphPoint.x > Position.x + Size.x - r && graphPoint.y > Position.y + Size.y - r)
            {
                float dx = graphPoint.x - (Position.x + Size.x - r);
                float dy = graphPoint.y - (Position.y + Size.y - r);
                return dx * dx + dy * dy <= r * r;
            }

            return true;
        }

        public void UpdateTransform(float zoom, Vector2 panOffset)
        {
            Vector2 screenPos = Position * zoom + panOffset;
            Vector2 scaledSize = Size * zoom;

            style.left = screenPos.x;
            style.top = screenPos.y;
            style.width = scaledSize.x;
            style.height = scaledSize.y;

            int scaledRadius = Mathf.Max(1, Mathf.RoundToInt(BaseCornerRadius * zoom));
            float borderWidth = Mathf.Max(1f, PermanentBorderWidth * zoom);

            _fill.style.borderTopLeftRadius = scaledRadius;
            _fill.style.borderTopRightRadius = scaledRadius;
            _fill.style.borderBottomLeftRadius = scaledRadius;
            _fill.style.borderBottomRightRadius = scaledRadius;

            _fill.style.borderLeftWidth = borderWidth;
            _fill.style.borderRightWidth = borderWidth;
            _fill.style.borderTopWidth = borderWidth;
            _fill.style.borderBottomWidth = borderWidth;

            _shadow.style.left = 0;
            _shadow.style.top = 0;
            _shadow.style.width = scaledSize.x;
            _shadow.style.height = scaledSize.y;
            _shadow.style.borderTopLeftRadius = scaledRadius;
            _shadow.style.borderTopRightRadius = scaledRadius;
            _shadow.style.borderBottomLeftRadius = scaledRadius;
            _shadow.style.borderBottomRightRadius = scaledRadius;

            if (_isActive)
            {
                float glowExpand = GlowExpandPx * zoom;
                _glow.style.left = -glowExpand;
                _glow.style.top = -glowExpand;
                _glow.style.width = scaledSize.x + glowExpand * 2f;
                _glow.style.height = scaledSize.y + glowExpand * 2f;

                int glowRadius = Mathf.RoundToInt((BaseCornerRadius + GlowBlurKernel) * zoom);
                _glow.style.borderTopLeftRadius = glowRadius;
                _glow.style.borderTopRightRadius = glowRadius;
                _glow.style.borderBottomLeftRadius = glowRadius;
                _glow.style.borderBottomRightRadius = glowRadius;
            }

            _nameLabel.style.fontSize = Mathf.RoundToInt(12 * zoom);
            _nameLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

            _subIcon.style.display = IsSubStateMachine ? DisplayStyle.Flex : DisplayStyle.None;
            if (IsSubStateMachine)
            {
                int iconSize = Mathf.RoundToInt(14 * zoom);
                _subIcon.style.right = Mathf.RoundToInt(4 * zoom);
                _subIcon.style.top = Mathf.RoundToInt(2 * zoom);
                _subIcon.style.fontSize = iconSize;
            }

            if (IsEditing)
            {
                int fontSize = Mathf.RoundToInt(12 * zoom);
                _editField.style.fontSize = fontSize;
                if (_editFieldInput != null)
                    _editFieldInput.style.fontSize = fontSize;
            }
        }

        public void StartEditing()
        {
            if (IsEditing) return;

            IsEditing = true;
            EditingBuffer = Name;

            _nameLabel.style.display = DisplayStyle.None;
            _editField.value = Name;
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

            _nameLabel.style.display = DisplayStyle.Flex;
            _editField.style.display = DisplayStyle.None;

            if (newName != oldName && !string.IsNullOrEmpty(newName))
            {
                Name = newName;
                EditingCommitted?.Invoke(this, oldName, newName);
            }
            else
            {
                Name = oldName;
                EditingCommitted?.Invoke(this, oldName, oldName);
            }
        }

        public void CancelEditing()
        {
            if (!IsEditing) return;

            IsEditing = false;
            Name = EditingBuffer;

            _nameLabel.style.display = DisplayStyle.Flex;
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

        public void ReactivateFlash()
        {
            if (_isActive)
                _activatedAtTime = Time.realtimeSinceStartup;
        }

        public void UpdateSubStateMachineVisual()
        {
            if (_fill != null)
            {
                _fill.EnableInClassList("state-view__fill--sub", IsSubStateMachine);
                _fill.EnableInClassList("state-view__fill--sub-entry", IsSubEntry);
                _fill.EnableInClassList("state-view__fill--entry", IsEntry && !IsSubEntry);
            }
            if (_subIcon != null)
                _subIcon.style.display = IsSubStateMachine ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void InitializeGlowAnimation()
        {
            schedule.Execute(() =>
            {
                if (_isActive)
                {
                    float flashElapsed = (float)(Time.realtimeSinceStartup - _activatedAtTime);
                    float flashDuration = 0.25f;
                    float flashBoost = 0f;
                    if (flashElapsed < flashDuration)
                    {
                        float t = flashElapsed / flashDuration;
                        flashBoost = (1f - t) * (1f - t);
                    }

                    float pulse = (Mathf.Sin((float)(Time.realtimeSinceStartup * GlowPulseSpeed)) + 1f) * 0.5f;
                    float minAlpha = 0.35f;
                    float maxAlpha = 0.85f;
                    float baseAlpha = minAlpha + pulse * (maxAlpha - minAlpha);
                    _glow.style.opacity = Mathf.Min(1f, baseAlpha + flashBoost * (1f - baseAlpha));
                }
                else if (_wasBriefActive)
                {
                    float elapsed = (float)(Time.realtimeSinceStartup - _deactivatedAtTime);
                    float blinkDuration = 0.25f;
                    if (elapsed < blinkDuration)
                    {
                        float t = elapsed / blinkDuration;
                        float blinkOpacity = 1f - t;
                        _glow.style.opacity = blinkOpacity;
                        if (_fill != null)
                            _fill.EnableInClassList("state-view__fill--active", true);
                    }
                    else
                    {
                        _wasBriefActive = false;
                        _glow.style.opacity = 0f;
                        if (_fill != null)
                            _fill.EnableInClassList("state-view__fill--active", false);
                    }
                }
                else
                {
                    _glow.style.opacity = 0f;
                }
            }).Every(30);
        }

        void ISelectable.DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
        }
    }
}
