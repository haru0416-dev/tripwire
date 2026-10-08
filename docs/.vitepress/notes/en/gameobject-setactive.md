**Show** on shows the objects, off hides them. Besides a fixed value, **Show** can take an On/Off variable or a value the event brings; with a synced variable, objects follow the variable.

A trigger on a hidden object no longer gets events. Hiding the trigger's own object stops its later events too.

Hiding an object also stops its renderers, colliders, Rigidbodies and scripts. Showing it again runs [OnEnable](/en/reference/events/onenable); hiding it runs [OnDisable](/en/reference/events/ondisable).

Showing a child whose parent is hidden doesn't make it visible. Only the child's own checkbox turns on, and it appears once all its parents are shown.

Official docs: [GameObject.SetActive (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.SetActive.html)
