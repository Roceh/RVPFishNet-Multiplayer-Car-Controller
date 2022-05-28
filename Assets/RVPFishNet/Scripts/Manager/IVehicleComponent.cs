using FishNet.Serializing;
using System.Collections.Generic;
using System.Text;

namespace RVP
{
    public interface IVehicleComponent
    {
        // Enables or disables component
        void SetActive(bool state);

        // Run the simulation step for vehicle script
        void Simulate();

        // Get the full state required to simulate next step and visualise vehicle
        void GetFullState(Writer writer);

        // Set the full state required to simulate next step and visualise vehicle
        void SetFullState(Reader reader);

        // Get the state required to visualise vehicle (not used)
        void GetVisualState(Writer writer);

        // Set the state required to visualise vehicle (not used)
        void SetVisualState(Reader reader);
    }
}