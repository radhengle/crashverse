using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>Light orbit assist around look target; primary aim is on the Wrecker barrel.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 pivotOffset = new Vector3(5f, 6f, 0f);
        public float distance = 22f;
        public float minPitch = 15f;
        public float maxPitch = 55f;
        public float yaw = 35f;
        public float pitch = 28f;
        public float sens = 0.12f;
        public bool dualFingerOrbit = true;

        void LateUpdate()
        {
            if (target == null) return;
            // Two-finger / right-mouse orbit (does not consume shots)
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * sens * 10f;
                pitch -= Input.GetAxis("Mouse Y") * sens * 10f;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            transform.position = pivot + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(pivot);
        }
    }
}
