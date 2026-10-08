Happens when a Contact Sender starts touching a VRC Contact Receiver in the world. The event only reaches UdonBehaviours on the Receiver's own object, so put the Tripwire Trigger on that object.

A touch counts only when the Sender and the Receiver share at least one entry in Collision Tags. Tags are case-sensitive, up to 16 per Contact. Humanoid avatars get Senders with tags like Head and Hand automatically. For a button pressed by hand, give the Receiver tags such as Hand or Finger and pick avatars in Content Types.

**The contact** holds the Sender that touched (contactSender), where it touched (contactPoint), how fast (enterVelocity) and the shared tags (matchingTags). contactSender's player tells whose avatar it was; for a Sender in the world it is empty. Read these with **Call Udon API**, using this value as the target.

The official docs say to check that contactSender's isValid is on before using the rest. A Contact's shape is limited to a 3 m radius and 6 m in width, height and depth.

Official docs: [Contacts (VRChat)](https://creators.vrchat.com/common-components/contacts/), [Built-In Contact Tags (VRChat)](https://creators.vrchat.com/common-components/contacts/built-in-contact-tags/)
