using FishNet.Object;
using System.Collections;
using UnityEngine;

namespace RVP
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Vehicle Controllers/Vehicle Debug", 3)]
    public class VehicleDebug : NetworkBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public Vector3 spawnPos;

        [Tooltip("")]
        public Vector3 spawnRot;

        [Tooltip("Y position below which the vehicle will be reset")]
        public float fallLimit = -20;

        private void Update()
        {
            if (Input.GetButtonDown("Reset Rotation"))
            {
                ResetRotation();
            }

            if (Input.GetButtonDown("Reset Position") || transform.position.y < fallLimit)
            {
                ResetPosition();
            }
        }

        // This waits for the next fixed update before resetting the rotation of the vehicle
        [ServerRpc]
        private void ResetRotation()
        {
            if (GetComponent<VehicleDamage>())
            {
                GetComponent<VehicleDamage>().Repair();
            }

            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            transform.Translate(Vector3.up, Space.World);
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }

        // This waits for the next fixed update before resetting the position of the vehicle
        [ServerRpc]
        private void ResetPosition()
        {
            if (GetComponent<VehicleDamage>())
            {
                GetComponent<VehicleDamage>().Repair();
            }

            transform.position = spawnPos;
            transform.rotation = Quaternion.LookRotation(spawnRot, GlobalControl.worldUpDir);
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }
    }
}