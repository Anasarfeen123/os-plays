# Embedded, patched com.unity.xr.hands 1.9.0

Embedded because 1.9.0's `XRHandSkeletonDriver.OnAfterDeserialize` compares `UnityEngine.Object`s, which throws on
Unity 6.3's scene-loading thread (`EnsureRunningOnMainThread can only be called from the main thread`). The throw
left the hand-skeleton joint map half-built, so tracked-hand visuals could fail to animate.

Patch (`Runtime/XRHandSkeletonDriver.cs`): `OnAfterDeserialize` does nothing; `OnEnable` calls
`InitializeFromSerializedReferences()` on the main thread. Documentation~ and Samples~ are left out.
To go back to the registry version, delete this folder and add `"com.unity.xr.hands": "<version>"` to
Packages/manifest.json once a fixed release is out.
