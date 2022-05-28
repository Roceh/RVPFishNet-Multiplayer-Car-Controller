using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Input/Basic Input", 0)]
    public class BasicInput : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public string accelAxis;

        [Tooltip("")]
        public string brakeAxis;

        [Tooltip("")]
        public string steerAxis;

        [Tooltip("")]
        public string ebrakeAxis;

        [Tooltip("")]
        public string boostButton;

        [Tooltip("")]
        public string upshiftButton;

        [Tooltip("")]
        public string downshiftButton;

        [Tooltip("")]
        public string pitchAxis;

        [Tooltip("")]
        public string yawAxis;

        [Tooltip("")]
        public string rollAxis;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        public struct MoveData
        {
            public float AccelInput;
            public float BrakeInput;
            public float SteerInput;
            public float EbrakeInput;
            public bool BoostButton;
            public bool UpshiftButton;
            public float UpshiftInput;
            public bool DownshiftButton;
            public float DownshiftInput;
            public float PitchInput;
            public float YawInput;
            public float RollInput;
            public float Burnout;
        }  

        public void CheckInput(out MoveData md)
        {
            md = default;

            // Get constant inputs
            if (!string.IsNullOrEmpty(accelAxis))
            {
                md.AccelInput = Input.GetAxis(accelAxis);
            }

            if (!string.IsNullOrEmpty(brakeAxis))
            {
                md.BrakeInput = Input.GetAxis(brakeAxis);
            }

            if (!string.IsNullOrEmpty(steerAxis))
            {
                md.SteerInput = Input.GetAxis(steerAxis);
            }

            if (!string.IsNullOrEmpty(ebrakeAxis))
            {
                md.EbrakeInput = Input.GetAxis(ebrakeAxis);
            }

            if (!string.IsNullOrEmpty(boostButton))
            {
                md.BoostButton = Input.GetButton(boostButton);
            }

            if (!string.IsNullOrEmpty(pitchAxis))
            {
                md.PitchInput = Input.GetAxis(pitchAxis);
            }

            if (!string.IsNullOrEmpty(yawAxis))
            {
                md.YawInput = Input.GetAxis(yawAxis);
            }

            if (!string.IsNullOrEmpty(rollAxis))
            {
                md.RollInput = Input.GetAxis(rollAxis);
            }

            if (!string.IsNullOrEmpty(upshiftButton))
            {
                md.UpshiftButton = Input.GetButton(upshiftButton);
                md.UpshiftInput = Input.GetAxis(upshiftButton);
            }

            if (!string.IsNullOrEmpty(downshiftButton))
            {
                md.DownshiftButton = Input.GetButton(downshiftButton);
                md.DownshiftInput= Input.GetAxis(downshiftButton);
            }
        }
    }
}