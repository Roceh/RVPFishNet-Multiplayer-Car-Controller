using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Camera/Basic Camera Input", 1)]
    public class BasicCameraInput : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public string xInputAxis;

        [Tooltip("")]
        public string yInputAxis;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private CameraControl _cam;

        private void Start()
        {
            // Get camera controller
            _cam = GetComponent<CameraControl>();
        }

        private void FixedUpdate()
        {
            // Set camera rotation input if the input axes are valid
            if (_cam && !string.IsNullOrEmpty(xInputAxis) && !string.IsNullOrEmpty(yInputAxis))
            {
                _cam.SetInput(Input.GetAxis(xInputAxis), Input.GetAxis(yInputAxis));
            }
        }
    }
}