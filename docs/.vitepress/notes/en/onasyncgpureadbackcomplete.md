Happens when a readback of a GPU texture, requested with VRCAsyncGPUReadback.Request, has finished. Only the UdonBehaviour named in the request receives it. The readback runs without blocking the main thread, so it doesn't drop the frame rate.

Check **The readback**'s hasError first. Get the data with TryGetData, not GetData.

Tripwire has no action that requests a readback. This event happens when another UdonSharp script makes the request with this trigger as the receiver.

Official docs: [AsyncGPUReadback (VRChat)](https://creators.vrchat.com/worlds/udon/vrc-graphics/asyncgpureadback/)
