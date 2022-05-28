using FishNet.Serializing;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RVP
{
    public class VehicleManager : MonoBehaviour
    {
        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public float tickDelta;

        // -=-=-=-= LOCAL STATE =-=-=-=-

        private List<VehicleComponentInfo> _vehicleComponents = new List<VehicleComponentInfo>();

        public struct VehicleComponentInfo
        {
            public IVehicleComponent VehicleComponent;

            public int Order;
        }

        public void RegisterVehicleComponent(IVehicleComponent vehicleComponent, int order)
        {
            _vehicleComponents.Add(new VehicleComponentInfo { VehicleComponent = vehicleComponent, Order = order });
            _vehicleComponents = _vehicleComponents.OrderBy(x => x.Order).ToList(); // i know!
        }

        public void Simulate(float inTickDelta)
        {
            tickDelta = inTickDelta;

            foreach (var item in _vehicleComponents)
                item.VehicleComponent.Simulate();
        }

        public void GetFullState(Writer writer)
        {
            foreach (var item in _vehicleComponents)
                item.VehicleComponent.GetFullState(writer);
        }

        public void SetFullState(Reader reader)
        {
            foreach (var item in _vehicleComponents)
                item.VehicleComponent.SetFullState(reader);
        }

        public void GetVisualState(Writer writer)
        {
            foreach (var item in _vehicleComponents)
                item.VehicleComponent.GetVisualState(writer);
        }

        public void SetVisualState(Reader reader)
        {
            foreach (var item in _vehicleComponents)
                item.VehicleComponent.SetVisualState(reader);
        }

        public void SetActive(bool state)
        {
            foreach (var item in _vehicleComponents)
                item.VehicleComponent.SetActive(state);
        }
    }
}