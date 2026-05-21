using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class SelectionBox
    {
        public bool IsActive { get; private set; }

        private Vector2 _startGraphPos;
        private Vector2 _endGraphPos;

        public void Start(Vector2 graphPos)
        {
            IsActive = true;
            _startGraphPos = graphPos;
            _endGraphPos = graphPos;
        }

        public void Update(Vector2 graphPos)
        {
            _endGraphPos = graphPos;
        }

        public void End()
        {
            IsActive = false;
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

            var rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

            Color fill = new Color(0.60f, 0.60f, 0.60f, 0.08f);
            Color border = new Color(0.60f, 0.60f, 0.60f, 0.5f);

            EditorGUI.DrawRect(rect, fill);

            float bw = 1f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, bw), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - bw, rect.width, bw), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, bw, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - bw, rect.y, bw, rect.height), border);
        }
    }
}
