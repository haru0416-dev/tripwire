The event for being taken out of a VRC Object Pool, but the official docs mark it deprecated. To do something when an object is spawned from a pool, use OnEnable.

A VRC Object Pool syncs whether each of its objects is active. Only the pool's owner can spawn (TryToSpawn) and return (Return) objects. Returned objects are disabled automatically, and players who join later see the same objects active or inactive.

Official docs: [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/)
