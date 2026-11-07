using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed class IslandCamera : MonoBehaviour
    {
        private Camera _camera;
        private Bounds _land;
        private float _yaw = 35f;
        private float _pitch = 55f;
        private float _zoom = 1f;
        private bool _orbiting;
        private Vector3 _pan;

        public void Frame(Camera camera, Bounds land)
        {
            _camera = camera;
            _land = land;
            _camera.orthographic = true;
            if (land.size.x > 200f)
            {
                _yaw = 24f;
                _pitch = 47f;
                _zoom = .93f;
                _pan = new Vector3(0f, -8f, 0f);
            }
            UpdatePose();
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            if (Input.GetMouseButtonDown(1))
                _orbiting = !GameBootstrap.MouseInsideHud();
            if (!Input.GetMouseButton(1))
                _orbiting = false;

            if (_orbiting)
            {
                _yaw += Input.GetAxis("Mouse X") * 4f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 3f, 32f, 78f);
            }

            if (!GameBootstrap.MouseInsideHud())
                _zoom = Mathf.Clamp(_zoom * Mathf.Exp(-Input.mouseScrollDelta.y * .12f), .06f, 1.35f);

            UpdatePose();
        }

        private void UpdatePose()
        {
            var dock = GameBootstrap.DockWidth / Mathf.Max(1, Screen.width);
            var header = GameBootstrap.HeaderHeight / Mathf.Max(1, Screen.height);
            _camera.rect = new Rect(dock, 0, Mathf.Max(.01f, 1f - dock), Mathf.Max(.01f, 1f - header));

            var yawRotation = Quaternion.Euler(0,_yaw,0);
            var motion = new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            _pan += yawRotation * motion * (_land.size.x*.25f*Time.deltaTime);
            _pan.x = Mathf.Clamp(_pan.x,-_land.extents.x,_land.extents.x);
            _pan.z = Mathf.Clamp(_pan.z,-_land.extents.z,_land.extents.z);
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var horizontal = ProjectedExtent(right);
            var vertical = ProjectedExtent(up);
            var aspect = _camera.pixelWidth / (float)Mathf.Max(1, _camera.pixelHeight);
            _camera.orthographicSize = Mathf.Max(vertical, horizontal / Mathf.Max(.01f, aspect))
                * 1.06f * _zoom;

            var distance = _land.extents.magnitude + 12f;
            _camera.farClipPlane = distance * 3f;
            transform.SetPositionAndRotation(_land.center + _pan - rotation * Vector3.forward * distance, rotation);
        }

        private float ProjectedExtent(Vector3 axis)
        {
            var extents = _land.extents;
            return Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y +
                Mathf.Abs(axis.z) * extents.z;
        }
    }
}
