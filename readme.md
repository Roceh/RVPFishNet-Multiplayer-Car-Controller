# Randomation Vehicle Physics ported to use FishNet client side predication

This is my attempt at trying to adapt the open source Randomation Vehicle Physics car controller to FishNet using client side predication.


https://user-images.githubusercontent.com/105083894/170823831-cbab49eb-54c6-4438-b918-ac31fc4a2c3d.mp4


Project was done in Unity 2021.3.2f1

Some notes:

- RVP uses script execution order to determine simulation order - this was changed to use a manager class which the car components script auto register with.
- All vehicles are simulated on both client and server, even the other players vehicles are fully simulated. This is so that vehicle to vehicle impacts work correctly amongst other reasons.
- This makes use of the cached state idea by FGG, its crudely implemented currently but does work well. Both rigidbodies and the vehicles cache their state (currently at the same interval as reconcilation for simplicity) and this is used to restore the world state during reconcilation playback. This is why the sphere/car interactions are very smooth - even with large latency.
- Reconcilation rate is reduced from the global tick rate to reduce bandwidth.  The server reconcilation now occurs every 10th tick, and the global tick rate is at 50 - so the physics run smoothly. 
- This is not optimized at all, its quite greedy on the bandwidth currently due to the size of the state of the vehicle and does some rather large allocations because of this. Open to suggestions on the allocations side.

---
#### Free assets used

FishNet: https://assetstore.unity.com/packages/tools/network/fish-net-networking-evolved-207815

Randomation Vehicle Physics: https://github.com/JustInvoke/Randomation-Vehicle-Physics
