using UnityEngine;

namespace CleanStateMachine
{
    public interface ISelectable
    {
        bool IsSelected { get; set; }
        Vector2 Position { get; set; }
        Vector2 Size { get; }
        Rect GetGraphBounds();
        bool ContainsPoint(Vector2 graphPoint);
        void DrawSelectionOverlay(float zoom, Vector2 panOffset);
    }
}
