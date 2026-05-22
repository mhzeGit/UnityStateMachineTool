using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class SelectionBox
    {
        public bool IsActive { get; private set; }

        private Vector2 _startGraphPos;
        private Vector2 _endGraphPos;

        public VisualElement Element { get; }

        public SelectionBox()
        {
            Element = new VisualElement();
            Element.pickingMode = PickingMode.Ignore;
            Element.style.position = Position.Absolute;
            Element.style.display = DisplayStyle.None;

            Color fill = new Color(0.5f, 0.7f, 1.0f, 0.12f);
            Color border = new Color(0.5f, 0.7f, 1.0f, 0.6f);

            Element.style.backgroundColor = fill;
            Element.style.borderLeftColor = border;
            Element.style.borderRightColor = border;
            Element.style.borderTopColor = border;
            Element.style.borderBottomColor = border;
            Element.style.borderLeftWidth = 1f;
            Element.style.borderRightWidth = 1f;
            Element.style.borderTopWidth = 1f;
            Element.style.borderBottomWidth = 1f;
        }

        public void Start(Vector2 graphPos)
        {
            IsActive = true;
            _startGraphPos = graphPos;
            _endGraphPos = graphPos;
            Element.style.display = DisplayStyle.Flex;
        }

        public void Update(Vector2 graphPos)
        {
            _endGraphPos = graphPos;
        }

        public void End()
        {
            IsActive = false;
            Element.style.display = DisplayStyle.None;
        }

        public Rect GetGraphRect()
        {
            float xMin = Mathf.Min(_startGraphPos.x, _endGraphPos.x);
            float xMax = Mathf.Max(_startGraphPos.x, _endGraphPos.x);
            float yMin = Mathf.Min(_startGraphPos.y, _endGraphPos.y);
            float yMax = Mathf.Max(_startGraphPos.y, _endGraphPos.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        public void DrawScreen(float zoom, Vector2 panOffset)
        {
            if (!IsActive)
                return;

            Vector2 startScreen = _startGraphPos * zoom + panOffset;
            Vector2 endScreen = _endGraphPos * zoom + panOffset;

            float xMin = Mathf.Min(startScreen.x, endScreen.x);
            float xMax = Mathf.Max(startScreen.x, endScreen.x);
            float yMin = Mathf.Min(startScreen.y, endScreen.y);
            float yMax = Mathf.Max(startScreen.y, endScreen.y);

            Element.style.left = xMin;
            Element.style.top = yMin;
            Element.style.width = xMax - xMin;
            Element.style.height = yMax - yMin;
        }
    }
}
