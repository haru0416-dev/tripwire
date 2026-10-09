Pick a variable of a component type (Rigidbody, AudioSource...) in **Variable** and an object in **Object**. The variable's type says which component to get. **Look in** set to **Children too** also searches the children, and **Parents too** the parents; both start with the object itself.

Objects placed in the scene can be picked directly in other actions' fields. This action helps with objects found while playing: the object that entered an area in On Trigger Enter, or one found with **Call Udon API**, can give its Rigidbody to move it.

When nothing is found, the variable is empty (null), and actions that use it then do nothing.

Official docs: [GetComponent (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.GetComponent.html), [GetComponentInChildren (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.GetComponentInChildren.html), [GetComponentInParent (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.GetComponentInParent.html)
