#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace RVP
{
    [CustomEditor(typeof(GasMotor))]
    [CanEditMultipleObjects]

    public class GasMotorEditor : Editor
    {
        private float _topSpeed = 0;

        public override void OnInspectorGUI() {
            GasMotor targetScript = (GasMotor)target;
            DriveForce nextOutput;
            Transmission nextTrans;
            GearboxTransmission nextGearbox;
            ContinuousTransmission nextConTrans;
            Suspension nextSus;
            bool reachedEnd = false;
            string endOutput = "";

            if (targetScript.outputDrives != null) {
                if (targetScript.outputDrives.Length > 0) {
                    _topSpeed = targetScript.torqueCurve.keys[targetScript.torqueCurve.length - 1].time * 1000;
                    nextOutput = targetScript.outputDrives[0];

                    while (!reachedEnd) {
                        if (nextOutput) {
                            if (nextOutput.GetComponent<Transmission>()) {
                                nextTrans = nextOutput.GetComponent<Transmission>();

                                if (nextTrans is GearboxTransmission) {
                                    nextGearbox = (GearboxTransmission)nextTrans;
                                    _topSpeed /= nextGearbox.gears[nextGearbox.gears.Length - 1].ratio;
                                }
                                else if (nextTrans is ContinuousTransmission) {
                                    nextConTrans = (ContinuousTransmission)nextTrans;
                                    _topSpeed /= nextConTrans.maxRatio;
                                }

                                if (nextTrans.outputDrives.Length > 0) {
                                    nextOutput = nextTrans.outputDrives[0];
                                }
                                else {
                                    _topSpeed = -1;
                                    reachedEnd = true;
                                    endOutput = nextTrans.transform.name;
                                }
                            }
                            else if (nextOutput.GetComponent<Suspension>()) {
                                nextSus = nextOutput.GetComponent<Suspension>();

                                if (nextSus.wheel) {
                                    _topSpeed /= Mathf.PI * 100;
                                    _topSpeed *= nextSus.wheel.tireRadius * 2 * Mathf.PI;
                                }
                                else {
                                    _topSpeed = -1;
                                }

                                reachedEnd = true;
                                endOutput = nextSus.transform.name;
                            }
                            else {
                                _topSpeed = -1;
                                reachedEnd = true;
                                endOutput = targetScript.transform.name;
                            }
                        }
                        else {
                            _topSpeed = -1;
                            reachedEnd = true;
                            endOutput = targetScript.transform.name;
                        }
                    }
                }
                else {
                    _topSpeed = -1;
                    endOutput = targetScript.transform.name;
                }
            }
            else {
                _topSpeed = -1;
                endOutput = targetScript.transform.name;
            }

            if (_topSpeed == -1) {
                EditorGUILayout.HelpBox("Motor drive doesn't reach any wheels.  (Ends at " + endOutput + ")", MessageType.Warning);
            }
            else if (targets.Length == 1) {
                EditorGUILayout.LabelField("Top Speed (Estimate): " + (_topSpeed * 2.23694f).ToString("0.00") + " mph || " + (_topSpeed * 3.6f).ToString("0.00") + " km/h", EditorStyles.boldLabel);
            }

            DrawDefaultInspector();
        }
    }
}
#endif