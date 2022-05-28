using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace RVP
{
    [AddComponentMenu("RVP/Drivetrain/Drive Force", 3)]
    public class DriveForce : MonoBehaviour
    {
        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float rpm;

        [System.NonSerialized]
        public float torque;

        [System.NonSerialized]
        public AnimationCurve curve; // Torque curve

        [System.NonSerialized]
        public float feedbackRPM; // RPM sent back through the drivetrain

        [System.NonSerialized]
        public bool active = true;

        public void GetFullState(Writer writer)
        {
            // assumption curve will not change....
            writer.WriteSingle(rpm);
            writer.WriteSingle(torque);
            writer.WriteSingle(feedbackRPM);
            writer.WriteBoolean(active);
        }

        public void SetFullState(Reader reader)
        {
            // assumption curve will not change....
            rpm = reader.ReadSingle();
            torque = reader.ReadSingle();
            feedbackRPM = reader.ReadSingle();
            active = reader.ReadBoolean();
        }

        public void GetVisualState(Writer writer)
        {
            writer.WriteSingle(rpm);
        }

        public void SetVisualState(Reader reader)
        {
            rpm = reader.ReadSingle();
        }

        public void SetDrive(DriveForce from)
        {
            rpm = from.rpm;
            torque = from.torque;
            curve = from.curve;
        }

        // Same as previous, but with torqueFactor multiplier for torque
        public void SetDrive(DriveForce from, float torqueFactor)
        {
            rpm = from.rpm;
            torque = from.torque * torqueFactor;
            curve = from.curve;
        }
    }
}