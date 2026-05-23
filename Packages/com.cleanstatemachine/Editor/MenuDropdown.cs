using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public static class MenuDropdown
    {
        private static StyleSheet _styleSheet;

        private static StyleSheet GetStyleSheet()
        {
            if (_styleSheet == null)
                _styleSheet = ScriptReferenceUtility.LoadStyleSheet("MenuDropdown");
            return _styleSheet;
        }

        public static void Show(VisualElement root, Vector2 screenPosition, Action<IBuilder> build)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("menu-dropdown-overlay");

            var menu = new VisualElement();
            menu.AddToClassList("menu-dropdown");
            menu.style.left = screenPosition.x;
            menu.style.top = screenPosition.y;

            var ss = GetStyleSheet();
            if (ss != null)
            {
                overlay.styleSheets.Add(ss);
                menu.styleSheets.Add(ss);
            }

            var builder = new Builder(menu, overlay);
            build(builder);

            overlay.Add(menu);

            overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == overlay)
                {
                    overlay.RemoveFromHierarchy();
                    evt.StopPropagation();
                }
            });

            root.Add(overlay);

            bool positionAdjusted = false;
            menu.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (positionAdjusted) return;
                positionAdjusted = true;

                float menuW = menu.resolvedStyle.width;
                float menuH = menu.resolvedStyle.height;
                if (menuW <= 0 || menuH <= 0) return;

                float rootW = root.resolvedStyle.width;
                float rootH = root.resolvedStyle.height;
                float x = menu.style.left.value.value;
                float y = menu.style.top.value.value;

                if (x + menuW > rootW)
                    menu.style.left = Mathf.Max(0f, rootW - menuW);
                if (y + menuH > rootH)
                    menu.style.top = Mathf.Max(0f, rootH - menuH);
            });
        }

        public interface IBuilder
        {
            void AddItem(string label, Action action);
            void AddSeparator();
            void AddDisabledItem(string label);
        }

        private class Builder : IBuilder
        {
            private readonly VisualElement _menu;
            private readonly VisualElement _overlay;

            public Builder(VisualElement menu, VisualElement overlay)
            {
                _menu = menu;
                _overlay = overlay;
            }

            public void AddItem(string label, Action action)
            {
                var container = new VisualElement();
                container.style.height = 24;
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.paddingLeft = 12;
                container.style.paddingRight = 12;
                container.style.backgroundColor = Color.clear;
                container.style.borderLeftWidth = 0;
                container.style.borderRightWidth = 0;
                container.style.borderTopWidth = 0;
                container.style.borderBottomWidth = 0;
                container.style.marginLeft = 0;
                container.style.marginRight = 0;
                container.style.marginTop = 0;
                container.style.marginBottom = 0;
                container.style.flexShrink = 0;

                var lbl = new Label(label);
                lbl.style.color = new Color(0.8f, 0.8f, 0.8f);
                lbl.style.fontSize = 11;
                lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
                lbl.style.whiteSpace = WhiteSpace.NoWrap;
                lbl.style.overflow = Overflow.Hidden;
                lbl.style.unityFontStyleAndWeight = FontStyle.Normal;
                lbl.style.flexGrow = 1;
                lbl.style.marginLeft = 0;
                lbl.style.marginRight = 0;
                lbl.style.paddingLeft = 0;
                lbl.style.paddingRight = 0;
                lbl.style.backgroundColor = Color.clear;
                container.Add(lbl);

                var hoverColor = new Color(0.275f, 0.392f, 0.706f);
                container.RegisterCallback<PointerEnterEvent>(_ =>
                    container.style.backgroundColor = hoverColor);
                container.RegisterCallback<PointerLeaveEvent>(_ =>
                    container.style.backgroundColor = Color.clear);

                container.RegisterCallback<PointerDownEvent>(evt =>
                {
                    action?.Invoke();
                    _overlay.RemoveFromHierarchy();
                    evt.StopPropagation();
                });

                _menu.Add(container);
            }

            public void AddSeparator()
            {
                var sep = new VisualElement();
                sep.style.height = 1;
                sep.style.marginTop = 4;
                sep.style.marginBottom = 4;
                sep.style.marginLeft = 8;
                sep.style.marginRight = 8;
                sep.style.backgroundColor = new Color(0.235f, 0.235f, 0.235f);
                _menu.Add(sep);
            }

            public void AddDisabledItem(string label)
            {
                var container = new VisualElement();
                container.style.height = 24;
                container.style.paddingLeft = 12;
                container.style.paddingRight = 12;
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;

                var lbl = new Label(label);
                lbl.style.color = new Color(0.5f, 0.5f, 0.5f);
                lbl.style.fontSize = 11;
                lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
                lbl.style.flexGrow = 1;
                lbl.style.marginLeft = 0;
                lbl.style.marginRight = 0;
                lbl.style.paddingLeft = 0;
                lbl.style.paddingRight = 0;
                lbl.style.backgroundColor = Color.clear;
                container.Add(lbl);

                _menu.Add(container);
            }
        }
    }
}
