Happens when you press or release the Jump button: Space on desktop, usually a face button on controllers. If the player has remapped their controls, the event follows their mapping.

It fires on both press and release. To act only on press, put **Pressed** into a temporary variable with **Set Variable**, then run the actions inside an If that checks the variable is on.

Udon gets no input while VRChat's main menu, quick menu (desktop and mobile) or text input popup is open. Holding the button while opening a menu sends a release (off). Closing the menu while still holding it does not send a press (on).

Official docs: [Input Events (VRChat)](https://creators.vrchat.com/worlds/udon/input-events/)
