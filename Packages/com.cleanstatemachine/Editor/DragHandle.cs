using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class DragHandle : VisualElement
    {
        public DragHandle()
        {
            AddToClassList("drag-handle");

            for (int row = 0; row < 5; row++)
            {
                var dotRow = new VisualElement();
                dotRow.AddToClassList("drag-handle-row");
                for (int col = 0; col < 4; col++)
                {
                    var dot = new VisualElement();
                    dot.AddToClassList("drag-handle-dot");
                    dot.pickingMode = PickingMode.Ignore;
                    dotRow.Add(dot);
                }
                Add(dotRow);
            }
        }
    }
}
