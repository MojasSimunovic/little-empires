using UnityEngine;

namespace LittleEmpires.Unity
{
    /// <summary>
    /// Tiny orbit camera for the first vertical slice. Uses the classic Unity
    /// Input API; Project Settings > Player > Active Input Handling should be Both.
    /// </summary>
    public sealed class IslandCamera : MonoBehaviour
    {
        public Vector3 Target = Vector3.zero;
        public float Distance = 19f;
        public float Yaw = 44f;
        public float Pitch = 53f;

        public void SetTarget(Vector3 point)
        {
            Target = point;
            UpdatePose();
        }

        private void LateUpdate()
        {
            if (Input.GetMouseButton(1))
            {
                Yaw += Input.GetAxis("Mouse X") * 4f;
                Pitch = Mathf.Clamp(Pitch - Input.GetAxis("Mouse Y") * 3f, 25f, 78f);
            }

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
                Distance = Mathf.Clamp(Distance - scroll * 1.6f, 9f, 32f);

            UpdatePose();
        }

        private void UpdatePose()
        {
            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = Target - rotation * Vector3.forward * Distance;
            transform.rotation = rotation;
        }
    }
}
