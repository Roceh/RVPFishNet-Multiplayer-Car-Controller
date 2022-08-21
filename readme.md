# Randomation Vehicle Physics ported to use FishNet client side predication

This is my attempt at trying to adapt the open source Randomation Vehicle Physics car controller to FishNet using client side predication.


https://user-images.githubusercontent.com/105083894/170823831-cbab49eb-54c6-4438-b918-ac31fc4a2c3d.mp4


Project was done in Unity 2021.3.2f1

Some notes:

- RVP uses script execution order to determine simulation order - this was changed to use a manager class which the car components script auto register with.
- All vehicles are simulated on both client and server, even the other players vehicles are fully simulated. This is so that vehicle to vehicle impacts work correctly amongst other reasons.
- This uses a slightly modified predicted object for the spheres that fixed an update issue (fishnet fix pending) and also only sets the rb state during reconcilation.
- Reconcilation rate is reduced from the global tick rate to reduce bandwidth.  The server reconcilation now occurs every 10th tick, and the global tick rate is at 50 - so the physics run smoothly. 
- This is not optimized at all, its quite greedy on the bandwidth currently due to the size of the state of the vehicle and does some rather large allocations because of this. Open to suggestions on the allocations side.

---
#### Free assets used

FishNet: https://assetstore.unity.com/packages/tools/network/fish-net-networking-evolved-207815

Randomation Vehicle Physics: https://github.com/JustInvoke/Randomation-Vehicle-Physics

---
MIT License

Portions Copyright (c) 2022 David Dunscombe
Portions Copyright (c) 2021 Justin Couch

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
