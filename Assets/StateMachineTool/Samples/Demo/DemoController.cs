using StateMachineTool.Runtime;
using UnityEngine;

namespace StateMachineTool.Samples.Demo
{
    public class DemoController : MonoBehaviour
    {
        public StateMachineRunner stateMachineRunner;
        public GameObject targetObject;
        public float moveSpeed = 3f;
        public float nearDistance = 2f;

        private Vector3 startPosition;

        private void Awake()
        {
            if (stateMachineRunner == null)
                stateMachineRunner = GetComponent<StateMachineRunner>();

            startPosition = transform.position;
        }

        private void Update()
        {
            if (stateMachineRunner == null || !stateMachineRunner.IsRunning)
                return;

            var blackboard = stateMachineRunner.Blackboard;
            if (blackboard == null) return;

            if (targetObject != null && blackboard.HasVariable("DistanceToTarget"))
            {
                float distance = Vector3.Distance(transform.position, targetObject.transform.position);
                blackboard.SetFloat("DistanceToTarget", distance);
                blackboard.SetBool("IsNearTarget", distance <= nearDistance);
            }
        }

        private void OnGUI()
        {
            if (stateMachineRunner == null || !stateMachineRunner.IsRunning) return;

            var currentState = stateMachineRunner.CurrentState;
            var blackboard = stateMachineRunner.Blackboard;
            if (blackboard == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 220));
            GUI.Box(new Rect(0, 0, 320, 220), "");
            GUILayout.Space(10);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUILayout.Label("State Machine Demo", titleStyle);

            string stateName = currentState != null ? currentState.displayName : "None";
            GUILayout.Label($"Current State: <b>{stateName}</b>");

            GUILayout.Label($"Time: {Time.time:F2}");

            if (blackboard.TryGetFloat("DistanceToTarget", out float dist))
                GUILayout.Label($"Distance to Target: <b>{dist:F2}</b>");

            if (blackboard.TryGetBool("IsNearTarget", out bool near))
                GUILayout.Label($"Is Near Target: <b>{near}</b>");

            GUILayout.Space(10);

            string[] eventNames = { "OnApproach", "OnDepart", "OnReachTarget" };
            foreach (var eventName in eventNames)
            {
                if (GUILayout.Button($"Trigger: {eventName}"))
                {
                    stateMachineRunner.TriggerEvent(eventName);
                }
            }

            GUILayout.EndArea();
        }
    }
}
