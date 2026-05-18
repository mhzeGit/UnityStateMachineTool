using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class CleanStateMachineWindow : EditorWindow
    {
        [MenuItem("Tools/CleanStateMachine")]
        public static void ShowWindow()
        {
            var window = CreateWindow<CleanStateMachineWindow>();
            window.titleContent = new GUIContent("CleanStateMachine");
            window.Show();
        }

        [SerializeField] private Vector2 _panOffset;
        [SerializeField] private float _zoom = 1f;

        private GraphView _graphView;
        private GraphPanController _panController;

        private void OnEnable()
        {
            wantsMouseMove = true;
            _graphView = new GraphView();
            _panController = new GraphPanController();
        }

        private void OnGUI()
        {
            if (position.width < 1f || position.height < 1f)
                return;

            var rect = new Rect(0f, 0f, position.width, position.height);

            _panController.HandleInput(rect, ref _panOffset, ref _zoom);
            _graphView.Draw(rect, _panOffset, _zoom);

            if (_panController.IsPanning)
                Repaint();
        }
    }
}
