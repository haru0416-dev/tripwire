using System.Collections.Generic;
using System.Linq;

namespace Tripwire.Core
{
    public sealed class EventParam
    {
        public string Name;
        public ParamType Type;
        /// <summary>Udon API calls: how the parameter is passed (Type is the value's type).</summary>
        public ParamPass Pass;
        public EventParam(string name, ParamType type, ParamPass pass = ParamPass.In) { Name = name; Type = type; Pass = pass; }
        /// <summary>A variable receives this parameter's value from the call (`out` / `ref`).</summary>
        public bool Receives => Pass == ParamPass.Out || Pass == ParamPass.Ref;
    }

    /// <summary>
    /// How a call's parameter is passed: a value; a variable the call writes (`out`) or reads and writes (`ref`); or an
    /// array passed as a value whose contents the call fills in (LineRenderer.GetPositions).
    /// </summary>
    public enum ParamPass { In, Out, Ref, Fill }

    /// <summary>A part an event needs on the trigger's object; see <see cref="EventCatalog.Needs"/>.</summary>
    public enum EventNeed { Collider, Area, Pickup, Station, VideoPlayer, UiComponent }

    public enum EventShape
    {
        /// <summary>`public override void X(...)` on UdonSharpBehaviour.</summary>
        Override,
        /// <summary>Unity message such as Start/OnEnable: `void X(...)`.</summary>
        UnityMessage,
        /// <summary>A named public method other scripts/triggers can call.</summary>
        Custom,
        /// <summary>Fired by the generator when a variable changes (locally or by sync).</summary>
        VariableChanged,
        /// <summary>A Unity UI element (Button/Toggle/Slider) the editor wires to the generated behaviour.</summary>
        Ui,
        /// <summary>Another script calls the trigger back by a chosen name, after the trigger registers with it.</summary>
        Listen,
        /// <summary>Fires after some seconds, once or repeatedly (the generator schedules it).</summary>
        Timer,
    }

    public sealed class EventSpec
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public EventShape Shape;
        /// <summary>Method name in UdonSharp for Override/UnityMessage.</summary>
        public string Method;
        public EventParam[] Params = new EventParam[0];
        /// <summary>Name of the player parameter the PlayerFilter applies to, if any.</summary>
        public string PlayerParam;
        public bool HasInteractText;
        /// <summary>Ui events: the UI component type ("UnityEngine.UI.Toggle").</summary>
        public string UiType;
        /// <summary>Ui events with a value: the member read when the event fires ("isOn", "value").</summary>
        public string UiValueMember;
        /// <summary>Video events: only delivered to UdonBehaviours on the video player's own GameObject.</summary>
        public bool NeedsVideoPlayerOnSameObject;
        /// <summary>Fires every frame, every physics step or while something stays: must not go over the network.</summary>
        public bool Frequent;
        /// <summary>Sync events: they happen only on behaviours that sync variables.</summary>
        public bool IsSyncEvent => Id == EventCatalog.DeserializationId || Id == "OnPreSerialization" || Id == "OnPostSerialization";
        /// <summary>The block's Name is part of the event: the custom event's name, the watched variable, the callback, the timer.</summary>
        public bool UsesName => Shape == EventShape.Custom || Shape == EventShape.VariableChanged || Shape == EventShape.Listen || Shape == EventShape.Timer;
        public string Description;
    }

    public static class EventCatalog
    {
        static readonly ParamType PlayerT = ParamType.Of(ValueKind.Player);
        static readonly Dictionary<string, EventSpec> byId = new Dictionary<string, EventSpec>();
        static readonly List<EventSpec> all = new List<EventSpec>();

        public static IReadOnlyList<EventSpec> All => all;

        public const string InteractId = "Interact";
        public const string StartId = "Start";
        public const string CustomId = "Custom";
        public const string VariableChangedId = "OnVariableChanged";
        public const string TimerId = "Timer";
        public const string ScriptNotifiedId = "ScriptNotified";
        public const string DeserializationId = "OnDeserialization";

        static readonly List<EventSpec> menu = new List<EventSpec>();
        /// <summary>Events in menu order, each with its <see cref="EventSpec.Category"/> from <see cref="Texts.EventCategories"/>.</summary>
        public static IReadOnlyList<EventSpec> InMenuOrder => menu;

        /// <summary>
        /// Events that need this object to be an area. (OnTriggerEnter & co. are not here: they also fire for a solid
        /// object entering someone else's area, like a ball reaching a goal.)
        /// </summary>
        public static readonly string[] TriggerZoneEvents =
        {
            "OnPlayerTriggerEnter", "OnPlayerTriggerExit", "OnPlayerTriggerStay", "OnDroneTriggerEnter", "OnDroneTriggerExit", "OnDroneTriggerStay",
        };

        /// <summary>Whether the event needs this part on the trigger's own object (the Inspector offers to add it).</summary>
        public static bool Needs(EventSpec spec, EventNeed need)
        {
            switch (need)
            {
                case EventNeed.Collider: return spec.Id == InteractId;
                case EventNeed.Area: return TriggerZoneEvents.Contains(spec.Id);
                case EventNeed.Pickup: return spec.Category == "Pickup";
                case EventNeed.Station: return spec.Id == "OnStationEntered" || spec.Id == "OnStationExited";
                case EventNeed.VideoPlayer: return spec.NeedsVideoPlayerOnSameObject;
                case EventNeed.UiComponent: return spec.Shape == EventShape.Ui;
                default: return false;
            }
        }

        static void Layout(string category, params string[] ids)
        {
            foreach (var id in ids)
            {
                var s = byId[id];
                s.Category = category;
                menu.Add(s);
            }
        }

        /// <summary>
        /// Ids that were renamed, old → new, so data saved with the old id keeps working. Ids are saved in scenes:
        /// rename one only by adding it here.
        /// </summary>
        public static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>();

        public static EventSpec Get(string id)
        {
            if (id == null) return null;
            if (byId.TryGetValue(id, out var spec)) return spec;
            return Renamed.TryGetValue(id, out var now) && byId.TryGetValue(now, out spec) ? spec : null;
        }

        static void Add(EventSpec s)
        {
            byId.Add(s.Id, s);
            all.Add(s);
        }

        static void Override(string method, string category, string display, string description)
        {
            Add(new EventSpec { Id = method, Method = method, Shape = EventShape.Override, Category = category, DisplayName = display, Description = description });
        }

        static void PlayerOverride(string method, string category, string display, string description)
        {
            Add(new EventSpec
            {
                Id = method, Method = method, Shape = EventShape.Override, Category = category, DisplayName = display, Description = description,
                Params = new[] { new EventParam("player", PlayerT) }, PlayerParam = "player",
            });
        }

        static void Message(string method, string category, string display, string description, params EventParam[] ps)
        {
            Add(new EventSpec { Id = method, Method = method, Shape = EventShape.UnityMessage, Category = category, DisplayName = display, Description = description, Params = ps });
        }

        static EventCatalog()
        {
            Add(new EventSpec
            {
                Id = InteractId, Method = "Interact", Shape = EventShape.Override, Category = "Basic", DisplayName = "Interact",
                HasInteractText = true, Description = "The local player clicks/uses this object. Needs a Collider.",
            });
            Message("Start", "Basic", "Start", "Once when the world loads (on every client).");
            Message("OnEnable", "Basic", "OnEnable", "When this object becomes active.");
            Message("OnDisable", "Basic", "OnDisable", "When this object becomes inactive.");
            Add(new EventSpec
            {
                Id = CustomId, Shape = EventShape.Custom, Category = "Basic", DisplayName = "Custom",
                Description = "A named event, run by a Send Event action or by other scripts via SendCustomEvent.",
            });
            Add(new EventSpec
            {
                Id = VariableChangedId, Shape = EventShape.VariableChanged, Category = "Basic", DisplayName = "On Variable Changed",
                Description = "When a variable changes, including changes received from the network and the state late joiners receive.",
            });

            PlayerOverride("OnPlayerTriggerEnter", "Player", "On Player Trigger Enter", "A player enters this trigger collider.");
            PlayerOverride("OnPlayerTriggerExit", "Player", "On Player Trigger Exit", "A player leaves this trigger collider.");
            PlayerOverride("OnPlayerCollisionEnter", "Player", "On Player Collision Enter", "A player collides with this collider.");
            PlayerOverride("OnPlayerJoined", "Player", "On Player Joined", "A player joins the instance (also fires for players already there when you join).");
            PlayerOverride("OnPlayerLeft", "Player", "On Player Left", "A player leaves the instance.");
            PlayerOverride("OnPlayerRespawn", "Player", "On Player Respawn", "A player respawns with Respawn in their menu.");
            PlayerOverride("OnStationEntered", "Player", "On Station Entered", "A player sits in this station.");
            PlayerOverride("OnStationExited", "Player", "On Station Exited", "A player leaves this station.");
            PlayerOverride("OnOwnershipTransferred", "Network", "On Ownership Transferred", "Ownership of this object moves to a player.");

            Override("OnPickup", "Pickup", "On Pickup", "The local player picks this object up.");
            Override("OnDrop", "Pickup", "On Drop", "The local player drops this object.");
            Override("OnPickupUseDown", "Pickup", "On Pickup Use Down", "The local player presses Use while holding this.");
            Override("OnPickupUseUp", "Pickup", "On Pickup Use Up", "The local player releases Use while holding this.");

            Message("OnTriggerEnter", "Physics", "On Trigger Enter (Collider)", "A non-player collider enters this trigger.",
                new EventParam("other", ParamType.Object("UnityEngine.Collider")));
            Message("OnTriggerExit", "Physics", "On Trigger Exit (Collider)", "A non-player collider leaves this trigger.",
                new EventParam("other", ParamType.Object("UnityEngine.Collider")));

            Add(new EventSpec { Id = TimerId, Shape = EventShape.Timer, DisplayName = "Timer", Description = "Every few seconds (or once after them), optionally at random intervals. Keeps running while the object is hidden (add Stop Timer to OnDisable to pause it)." });
            Message("Update", "Advanced", "Update", "Every frame (keep the actions light).");
            Layout("Common", "Interact", "OnPlayerTriggerEnter", "OnPlayerTriggerExit", "Start", "Timer", "Custom", "OnVariableChanged");
            Layout("Pickup", "OnPickup", "OnDrop", "OnPickupUseDown", "OnPickupUseUp");
            Layout("Player", "OnPlayerJoined", "OnPlayerLeft", "OnPlayerRespawn", "OnStationEntered", "OnStationExited");
            Add(new EventSpec
            {
                Id = "UiButtonClick", Shape = EventShape.Ui, UiType = "UnityEngine.UI.Button", DisplayName = "UI Button Pressed",
                Description = "A UI Button is pressed. Tripwire wires its OnClick for you.",
            });
            Add(new EventSpec
            {
                Id = "UiToggleChanged", Shape = EventShape.Ui, UiType = "UnityEngine.UI.Toggle", UiValueMember = "isOn", DisplayName = "UI Toggle Changed",
                Description = "A UI Toggle is switched; its on/off state is the event's value.",
                Params = new[] { new EventParam("value", ParamType.Of(ValueKind.Bool)) },
            });
            Add(new EventSpec
            {
                Id = "UiSliderChanged", Shape = EventShape.Ui, UiType = "UnityEngine.UI.Slider", UiValueMember = "value", DisplayName = "UI Slider Moved",
                Description = "A UI Slider is moved; its value is the event's value.",
                Params = new[] { new EventParam("value", ParamType.Of(ValueKind.Float)) },
            });
            // VRChat video players (VRCUnityVideoPlayer / VRCAVProVideoPlayer) send these to the UdonBehaviours on their own GameObject.
            Override("OnVideoReady", "Video", "On Video Ready", "The video on this object's video player is loaded and ready.");
            Override("OnVideoStart", "Video", "On Video Start", "The video starts playing.");
            Override("OnVideoEnd", "Video", "On Video End", "The video finishes playing: it reached its end, or a player ended it.");
            Override("OnVideoLoop", "Video", "On Video Loop", "A looping video starts over.");
            Add(new EventSpec
            {
                Id = "OnVideoError", Method = "OnVideoError", Shape = EventShape.Override, Category = "Video", DisplayName = "On Video Error",
                Description = "The video failed to load or play; the error kind is the event's value.",
                Params = new[] { new EventParam("videoError", new ParamType(ValueKind.Enum, "VRC.SDK3.Components.Video.VideoError")) },
            });

            // Every other Udon event, with the values it brings.
            Add(new EventSpec { Id = "OnPlayerTriggerStay", Method = "OnPlayerTriggerStay", Shape = EventShape.Override, DisplayName = "On Player Trigger Stay", Description = "Every physics step while a player stays in this trigger collider.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerCollisionExit", Method = "OnPlayerCollisionExit", Shape = EventShape.Override, DisplayName = "On Player Collision Exit", Description = "A player stops touching this collider.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerCollisionStay", Method = "OnPlayerCollisionStay", Shape = EventShape.Override, DisplayName = "On Player Collision Stay", Description = "Every physics step while a player touches this collider.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerParticleCollision", Method = "OnPlayerParticleCollision", Shape = EventShape.Override, DisplayName = "On Player Particle Collision", Description = "A particle of this particle system hits a player.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnControllerColliderHitPlayer", Method = "OnControllerColliderHitPlayer", Shape = EventShape.Override, DisplayName = "On Controller Collider Hit Player", Description = "This object's CharacterController hits a player while moving.", Params = new EventParam[] { new EventParam("hit", ParamType.OtherType("VRC.SDK3.ControllerColliderPlayerHit")) } });
            Add(new EventSpec { Id = "OnTriggerStay", Method = "OnTriggerStay", Shape = EventShape.UnityMessage, DisplayName = "On Trigger Stay (Collider)", Description = "Every physics step while a non-player collider stays in this trigger.", Params = new EventParam[] { new EventParam("other", ParamType.Object("UnityEngine.Collider")) } });
            Add(new EventSpec { Id = "OnCollisionEnter", Method = "OnCollisionEnter", Shape = EventShape.UnityMessage, DisplayName = "On Collision Enter", Description = "A non-player collider hits this one (needs a Rigidbody on one of them).", Params = new EventParam[] { new EventParam("other", ParamType.OtherType("UnityEngine.Collision")) } });
            Add(new EventSpec { Id = "OnCollisionExit", Method = "OnCollisionExit", Shape = EventShape.UnityMessage, DisplayName = "On Collision Exit", Description = "A non-player collider stops touching this one.", Params = new EventParam[] { new EventParam("other", ParamType.OtherType("UnityEngine.Collision")) } });
            Add(new EventSpec { Id = "OnCollisionStay", Method = "OnCollisionStay", Shape = EventShape.UnityMessage, DisplayName = "On Collision Stay", Description = "Every physics step while a non-player collider touches this one.", Params = new EventParam[] { new EventParam("other", ParamType.OtherType("UnityEngine.Collision")) } });
            Add(new EventSpec { Id = "OnTriggerEnter2D", Method = "OnTriggerEnter2D", Shape = EventShape.UnityMessage, DisplayName = "On Trigger Enter 2D", Description = "A 2D collider enters this 2D trigger.", Params = new EventParam[] { new EventParam("other", ParamType.Object("UnityEngine.Collider2D")) } });
            Add(new EventSpec { Id = "OnTriggerExit2D", Method = "OnTriggerExit2D", Shape = EventShape.UnityMessage, DisplayName = "On Trigger Exit 2D", Description = "A 2D collider leaves this 2D trigger.", Params = new EventParam[] { new EventParam("other", ParamType.Object("UnityEngine.Collider2D")) } });
            Add(new EventSpec { Id = "OnTriggerStay2D", Method = "OnTriggerStay2D", Shape = EventShape.UnityMessage, DisplayName = "On Trigger Stay 2D", Description = "Every physics step while a 2D collider stays in this 2D trigger.", Params = new EventParam[] { new EventParam("other", ParamType.Object("UnityEngine.Collider2D")) } });
            Add(new EventSpec { Id = "OnCollisionEnter2D", Method = "OnCollisionEnter2D", Shape = EventShape.UnityMessage, DisplayName = "On Collision Enter 2D", Description = "A 2D collider hits this one.", Params = new EventParam[] { new EventParam("other", ParamType.OtherType("UnityEngine.Collision2D")) } });
            Add(new EventSpec { Id = "OnCollisionExit2D", Method = "OnCollisionExit2D", Shape = EventShape.UnityMessage, DisplayName = "On Collision Exit 2D", Description = "A 2D collider stops touching this one.", Params = new EventParam[] { new EventParam("other", ParamType.OtherType("UnityEngine.Collision2D")) } });
            Add(new EventSpec { Id = "OnCollisionStay2D", Method = "OnCollisionStay2D", Shape = EventShape.UnityMessage, DisplayName = "On Collision Stay 2D", Description = "Every physics step while a 2D collider touches this one.", Params = new EventParam[] { new EventParam("other", ParamType.OtherType("UnityEngine.Collision2D")) } });
            Add(new EventSpec { Id = "OnControllerColliderHit", Method = "OnControllerColliderHit", Shape = EventShape.UnityMessage, DisplayName = "On Controller Collider Hit", Description = "This object's CharacterController hits a collider while moving.", Params = new EventParam[] { new EventParam("hit", ParamType.OtherType("UnityEngine.ControllerColliderHit")) } });
            Add(new EventSpec { Id = "OnParticleCollision", Method = "OnParticleCollision", Shape = EventShape.UnityMessage, DisplayName = "On Particle Collision", Description = "A particle hits this object, or this particle system's particle hits another object.", Params = new EventParam[] { new EventParam("other", ParamType.Object("UnityEngine.GameObject")) } });
            Add(new EventSpec { Id = "OnParticleTrigger", Method = "OnParticleTrigger", Shape = EventShape.UnityMessage, DisplayName = "On Particle Trigger", Description = "Particles meet this particle system's Triggers module conditions.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnJointBreak", Method = "OnJointBreak", Shape = EventShape.UnityMessage, DisplayName = "On Joint Break", Description = "A joint on this object breaks; the force is the event's value.", Params = new EventParam[] { new EventParam("force", ParamType.Of(ValueKind.Float)) } });
            Add(new EventSpec { Id = "OnJointBreak2D", Method = "OnJointBreak2D", Shape = EventShape.UnityMessage, DisplayName = "On Joint Break 2D", Description = "A 2D joint on this object breaks.", Params = new EventParam[] { new EventParam("joint", ParamType.Object("UnityEngine.Joint2D")) } });
            Add(new EventSpec { Id = "OnTransformParentChanged", Method = "OnTransformParentChanged", Shape = EventShape.UnityMessage, DisplayName = "On Parent Changed", Description = "This object's parent changes.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnTransformChildrenChanged", Method = "OnTransformChildrenChanged", Shape = EventShape.UnityMessage, DisplayName = "On Children Changed", Description = "A child is added to or removed from this object.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "LateUpdate", Method = "LateUpdate", Shape = EventShape.UnityMessage, DisplayName = "Late Update", Description = "Every frame, after every Update.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "FixedUpdate", Method = "FixedUpdate", Shape = EventShape.UnityMessage, DisplayName = "Fixed Update", Description = "Every physics step (keep the actions light).", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "PostLateUpdate", Method = "PostLateUpdate", Shape = EventShape.Override, DisplayName = "Post Late Update", Description = "Every frame, after VRChat has moved the players (for following a player's bones).", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnBecameVisible", Method = "OnBecameVisible", Shape = EventShape.UnityMessage, DisplayName = "On Became Visible", Description = "This object's renderer comes into view of any camera.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnBecameInvisible", Method = "OnBecameInvisible", Shape = EventShape.UnityMessage, DisplayName = "On Became Invisible", Description = "This object's renderer is no longer seen by any camera.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnWillRenderObject", Method = "OnWillRenderObject", Shape = EventShape.UnityMessage, DisplayName = "On Will Render Object", Description = "Each camera is about to render this object.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnPreCull", Method = "OnPreCull", Shape = EventShape.UnityMessage, DisplayName = "On Pre Cull", Description = "A camera on this object is about to cull the scene.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnPreRender", Method = "OnPreRender", Shape = EventShape.UnityMessage, DisplayName = "On Pre Render", Description = "A camera on this object is about to render.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnPostRender", Method = "OnPostRender", Shape = EventShape.UnityMessage, DisplayName = "On Post Render", Description = "A camera on this object has rendered.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnRenderObject", Method = "OnRenderObject", Shape = EventShape.UnityMessage, DisplayName = "On Render Object", Description = "After a camera has rendered the scene.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnAnimatorMove", Method = "OnAnimatorMove", Shape = EventShape.UnityMessage, DisplayName = "On Animator Move", Description = "The Animator on this object has computed root motion.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnAnimatorIK", Method = "OnAnimatorIK", Shape = EventShape.UnityMessage, DisplayName = "On Animator IK", Description = "The Animator on this object is setting up IK; the layer is the event's value.", Params = new EventParam[] { new EventParam("layerIndex", ParamType.Of(ValueKind.Int)) } });
            Add(new EventSpec { Id = "OnDestroy", Method = "OnDestroy", Shape = EventShape.UnityMessage, DisplayName = "On Destroy", Description = "This object is destroyed.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnAsyncGpuReadbackComplete", Method = "OnAsyncGpuReadbackComplete", Shape = EventShape.Override, DisplayName = "On GPU Readback Complete", Description = "A GPU readback requested by this script has finished.", Params = new EventParam[] { new EventParam("request", ParamType.OtherType("VRC.SDK3.Rendering.VRCAsyncGPUReadbackRequest")) } });
            Add(new EventSpec { Id = "OnVRCCameraSettingsChanged", Method = "OnVRCCameraSettingsChanged", Shape = EventShape.Override, DisplayName = "On Camera Settings Changed", Description = "The player's camera settings change.", Params = new EventParam[] { new EventParam("cameraSettings", ParamType.OtherType("VRC.SDK3.Rendering.VRCCameraSettings")) } });
            Add(new EventSpec { Id = "OnVRCQualitySettingsChanged", Method = "OnVRCQualitySettingsChanged", Shape = EventShape.Override, DisplayName = "On Quality Settings Changed", Description = "The player's graphics quality settings change.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnScreenUpdate", Method = "OnScreenUpdate", Shape = EventShape.Override, DisplayName = "On Screen Update", Description = "On a mobile device: when entering the world, and when the screen orientation changes.", Params = new EventParam[] { new EventParam("data", ParamType.OtherType("VRC.SDK3.Platform.ScreenUpdateData", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnDeserialization", Method = "OnDeserialization", Shape = EventShape.Override, DisplayName = "On Deserialization", Description = "Synced variables arrive from the owner (also for late joiners).", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnPreSerialization", Method = "OnPreSerialization", Shape = EventShape.Override, DisplayName = "On Pre Serialization", Description = "Just before this object's synced variables are sent.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnPostSerialization", Method = "OnPostSerialization", Shape = EventShape.Override, DisplayName = "On Post Serialization", Description = "After this object's synced variables were sent; the result is the event's value.", Params = new EventParam[] { new EventParam("result", ParamType.OtherType("VRC.Udon.Common.SerializationResult", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnMasterTransferred", Method = "OnMasterTransferred", Shape = EventShape.Override, DisplayName = "On Master Transferred", Description = "The instance master changes.", Params = new EventParam[] { new EventParam("newMaster", PlayerT) }, PlayerParam = "newMaster" });
            Add(new EventSpec { Id = "OnPlayerRestored", Method = "OnPlayerRestored", Shape = EventShape.Override, DisplayName = "On Player Restored", Description = "A player's saved data (persistence) has been loaded.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerDataUpdated", Method = "OnPlayerDataUpdated", Shape = EventShape.Override, DisplayName = "On Player Data Updated", Description = "A player's saved data (PlayerData) changes.", Params = new EventParam[] { new EventParam("player", PlayerT), new EventParam("infos", ParamType.OtherType("VRC.SDK3.Persistence.PlayerData.Info[]")) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerDataStorageWarning", Method = "OnPlayerDataStorageWarning", Shape = EventShape.Override, DisplayName = "On Player Data Storage Warning", Description = "A player's PlayerData is close to its size limit.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerDataStorageExceeded", Method = "OnPlayerDataStorageExceeded", Shape = EventShape.Override, DisplayName = "On Player Data Storage Exceeded", Description = "A player's PlayerData is over its size limit.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerObjectStorageWarning", Method = "OnPlayerObjectStorageWarning", Shape = EventShape.Override, DisplayName = "On Player Object Storage Warning", Description = "A player's persistent objects are close to their size limit.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerObjectStorageExceeded", Method = "OnPlayerObjectStorageExceeded", Shape = EventShape.Override, DisplayName = "On Player Object Storage Exceeded", Description = "A player's persistent objects are over their size limit.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPersistenceUsageUpdated", Method = "OnPersistenceUsageUpdated", Shape = EventShape.Override, DisplayName = "On Persistence Usage Updated", Description = "The amount of saved data in use is updated.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnSpawn", Method = "OnSpawn", Shape = EventShape.Override, DisplayName = "On Spawn", Description = "This object was spawned from a VRC Object Pool. Deprecated by VRChat: use On Enable instead.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnAvatarChanged", Method = "OnAvatarChanged", Shape = EventShape.Override, DisplayName = "On Avatar Changed", Description = "A player changes avatar.", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnAvatarEyeHeightChanged", Method = "OnAvatarEyeHeightChanged", Shape = EventShape.Override, DisplayName = "On Avatar Eye Height Changed", Description = "A player's avatar height changes; the old height is the event's value.", Params = new EventParam[] { new EventParam("player", PlayerT), new EventParam("prevEyeHeightAsMeters", ParamType.Of(ValueKind.Float)) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPlayerSuspendChanged", Method = "OnPlayerSuspendChanged", Shape = EventShape.Override, DisplayName = "On Player Suspend Changed", Description = "A player's app is suspended or resumed (mobile).", Params = new EventParam[] { new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnContactEnter", Method = "OnContactEnter", Shape = EventShape.Override, DisplayName = "On Contact Enter", Description = "A contact sender touches a VRC Contact Receiver on this object.", Params = new EventParam[] { new EventParam("contactInfo", ParamType.OtherType("VRC.Dynamics.ContactEnterInfo", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnContactExit", Method = "OnContactExit", Shape = EventShape.Override, DisplayName = "On Contact Exit", Description = "A contact sender stops touching a VRC Contact Receiver on this object.", Params = new EventParam[] { new EventParam("contactInfo", ParamType.OtherType("VRC.Dynamics.ContactExitInfo", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnPhysBoneGrabbed", Method = "OnPhysBoneGrabbed", Shape = EventShape.Override, DisplayName = "On PhysBone Grabbed", Description = "A PhysBone on this object is grabbed.", Params = new EventParam[] { new EventParam("physBoneInfo", ParamType.OtherType("VRC.Dynamics.PhysBoneGrabbedInfo", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnPhysBoneReleased", Method = "OnPhysBoneReleased", Shape = EventShape.Override, DisplayName = "On PhysBone Released", Description = "A grabbed PhysBone on this object is released.", Params = new EventParam[] { new EventParam("physBoneInfo", ParamType.OtherType("VRC.Dynamics.PhysBoneReleasedInfo", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnPhysBonePosed", Method = "OnPhysBonePosed", Shape = EventShape.Override, DisplayName = "On PhysBone Posed", Description = "A PhysBone on this object is posed (left in place).", Params = new EventParam[] { new EventParam("physBoneInfo", ParamType.OtherType("VRC.Dynamics.PhysBonePosedInfo", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnPhysBoneUnPosed", Method = "OnPhysBoneUnPosed", Shape = EventShape.Override, DisplayName = "On PhysBone Unposed", Description = "A posed PhysBone on this object is let go.", Params = new EventParam[] { new EventParam("physBoneInfo", ParamType.OtherType("VRC.Dynamics.PhysBoneUnPosedInfo", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnDroneTriggerEnter", Method = "OnDroneTriggerEnter", Shape = EventShape.Override, DisplayName = "On Drone Trigger Enter", Description = "A player's drone (camera drone) enters this trigger.", Params = new EventParam[] { new EventParam("drone", ParamType.OtherType("VRC.SDKBase.VRCDroneApi")) } });
            Add(new EventSpec { Id = "OnDroneTriggerExit", Method = "OnDroneTriggerExit", Shape = EventShape.Override, DisplayName = "On Drone Trigger Exit", Description = "A drone leaves this trigger.", Params = new EventParam[] { new EventParam("drone", ParamType.OtherType("VRC.SDKBase.VRCDroneApi")) } });
            Add(new EventSpec { Id = "OnDroneTriggerStay", Method = "OnDroneTriggerStay", Shape = EventShape.Override, DisplayName = "On Drone Trigger Stay", Description = "Every physics step while a drone stays in this trigger.", Params = new EventParam[] { new EventParam("drone", ParamType.OtherType("VRC.SDKBase.VRCDroneApi")) } });
            Add(new EventSpec { Id = "InputJump", Method = "InputJump", Shape = EventShape.Override, DisplayName = "Input Jump", Description = "The local player presses or releases Jump; pressed is the event's value.", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Bool)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputUse", Method = "InputUse", Shape = EventShape.Override, DisplayName = "Input Use", Description = "The local player presses or releases Use.", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Bool)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputGrab", Method = "InputGrab", Shape = EventShape.Override, DisplayName = "Input Grab", Description = "The local player presses or releases Grab.", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Bool)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputDrop", Method = "InputDrop", Shape = EventShape.Override, DisplayName = "Input Drop", Description = "The local player presses or releases Drop.", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Bool)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputMoveHorizontal", Method = "InputMoveHorizontal", Shape = EventShape.Override, DisplayName = "Input Move Horizontal", Description = "The local player's left/right move input changes (-1 to 1).", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Float)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputMoveVertical", Method = "InputMoveVertical", Shape = EventShape.Override, DisplayName = "Input Move Vertical", Description = "The local player's forward/back move input changes (-1 to 1).", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Float)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputLookHorizontal", Method = "InputLookHorizontal", Shape = EventShape.Override, DisplayName = "Input Look Horizontal", Description = "The local player's left/right look input changes.", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Float)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "InputLookVertical", Method = "InputLookVertical", Shape = EventShape.Override, DisplayName = "Input Look Vertical", Description = "The local player's up/down look input changes.", Params = new EventParam[] { new EventParam("value", ParamType.Of(ValueKind.Float)), new EventParam("args", ParamType.OtherType("VRC.Udon.Common.UdonInputEventArgs", hasEquality: false)) } });
            Add(new EventSpec { Id = "OnInputMethodChanged", Method = "OnInputMethodChanged", Shape = EventShape.Override, DisplayName = "On Input Method Changed", Description = "The local player switches input (keyboard, controller, touch...).", Params = new EventParam[] { new EventParam("inputMethod", new ParamType(ValueKind.Enum, "VRC.SDKBase.VRCInputMethod")) } });
            Add(new EventSpec { Id = "OnLanguageChanged", Method = "OnLanguageChanged", Shape = EventShape.Override, DisplayName = "On Language Changed", Description = "When joining, and when the local player changes VRChat's language; the language code is the event's value.", Params = new EventParam[] { new EventParam("language", ParamType.Of(ValueKind.String)) } });
            Add(new EventSpec { Id = "MidiNoteOn", Method = "MidiNoteOn", Shape = EventShape.Override, DisplayName = "MIDI Note On", Description = "A MIDI key is pressed (channel, note number, velocity).", Params = new EventParam[] { new EventParam("channel", ParamType.Of(ValueKind.Int)), new EventParam("number", ParamType.Of(ValueKind.Int)), new EventParam("velocity", ParamType.Of(ValueKind.Int)) } });
            Add(new EventSpec { Id = "MidiNoteOff", Method = "MidiNoteOff", Shape = EventShape.Override, DisplayName = "MIDI Note Off", Description = "A MIDI key is released.", Params = new EventParam[] { new EventParam("channel", ParamType.Of(ValueKind.Int)), new EventParam("number", ParamType.Of(ValueKind.Int)), new EventParam("velocity", ParamType.Of(ValueKind.Int)) } });
            Add(new EventSpec { Id = "MidiControlChange", Method = "MidiControlChange", Shape = EventShape.Override, DisplayName = "MIDI Control Change", Description = "A MIDI knob or slider moves (channel, control number, value).", Params = new EventParam[] { new EventParam("channel", ParamType.Of(ValueKind.Int)), new EventParam("number", ParamType.Of(ValueKind.Int)), new EventParam("value", ParamType.Of(ValueKind.Int)) } });
            Add(new EventSpec { Id = "OnStringLoadSuccess", Method = "OnStringLoadSuccess", Shape = EventShape.Override, DisplayName = "On String Load Success", Description = "Text requested with VRCStringDownloader has arrived.", Params = new EventParam[] { new EventParam("result", ParamType.OtherType("VRC.SDK3.StringLoading.IVRCStringDownload")) } });
            Add(new EventSpec { Id = "OnStringLoadError", Method = "OnStringLoadError", Shape = EventShape.Override, DisplayName = "On String Load Error", Description = "Loading text with VRCStringDownloader failed.", Params = new EventParam[] { new EventParam("result", ParamType.OtherType("VRC.SDK3.StringLoading.IVRCStringDownload")) } });
            Add(new EventSpec { Id = "OnImageLoadSuccess", Method = "OnImageLoadSuccess", Shape = EventShape.Override, DisplayName = "On Image Load Success", Description = "An image requested with VRCImageDownloader has arrived.", Params = new EventParam[] { new EventParam("result", ParamType.OtherType("VRC.SDK3.Image.IVRCImageDownload")) } });
            Add(new EventSpec { Id = "OnImageLoadError", Method = "OnImageLoadError", Shape = EventShape.Override, DisplayName = "On Image Load Error", Description = "Loading an image with VRCImageDownloader failed.", Params = new EventParam[] { new EventParam("result", ParamType.OtherType("VRC.SDK3.Image.IVRCImageDownload")) } });
            Add(new EventSpec { Id = "OnVideoPlay", Method = "OnVideoPlay", Shape = EventShape.Override, DisplayName = "On Video Play", Description = "The video resumes or starts playing.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnVideoPause", Method = "OnVideoPause", Shape = EventShape.Override, DisplayName = "On Video Pause", Description = "The video is paused.", Params = new EventParam[] {  } });
            Add(new EventSpec { Id = "OnPurchaseConfirmed", Method = "OnPurchaseConfirmed", Shape = EventShape.Override, DisplayName = "On Purchase Confirmed", Description = "A player owns a product (bought now or before). Deprecated by VRChat: use On Purchase Confirmed Multiple instead.", Params = new EventParam[] { new EventParam("product", ParamType.OtherType("VRC.Economy.IProduct")), new EventParam("player", PlayerT), new EventParam("purchasedNow", ParamType.Of(ValueKind.Bool)) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPurchaseConfirmedMultiple", Method = "OnPurchaseConfirmedMultiple", Shape = EventShape.Override, DisplayName = "On Purchase Confirmed (Quantity)", Description = "A player owns some of a product that can be bought several times.", Params = new EventParam[] { new EventParam("product", ParamType.OtherType("VRC.Economy.IProduct")), new EventParam("player", PlayerT), new EventParam("purchasedNow", ParamType.Of(ValueKind.Bool)), new EventParam("quantity", ParamType.Of(ValueKind.Int)) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPurchaseExpired", Method = "OnPurchaseExpired", Shape = EventShape.Override, DisplayName = "On Purchase Expired", Description = "A player's product has expired.", Params = new EventParam[] { new EventParam("product", ParamType.OtherType("VRC.Economy.IProduct")), new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnProductEvent", Method = "OnProductEvent", Shape = EventShape.Override, DisplayName = "On Product Event", Description = "A player who owns a product sends its event (Store.SendProductEvent); everyone in the instance gets it.", Params = new EventParam[] { new EventParam("product", ParamType.OtherType("VRC.Economy.IProduct")), new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnPurchasesLoaded", Method = "OnPurchasesLoaded", Shape = EventShape.Override, DisplayName = "On Purchases Loaded", Description = "A player's purchases have been loaded.", Params = new EventParam[] { new EventParam("products", ParamType.OtherType("VRC.Economy.IProduct[]")), new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnListPurchases", Method = "OnListPurchases", Shape = EventShape.Override, DisplayName = "On List Purchases", Description = "The list of a player's purchases has arrived.", Params = new EventParam[] { new EventParam("products", ParamType.OtherType("VRC.Economy.IProduct[]")), new EventParam("player", PlayerT) }, PlayerParam = "player" });
            Add(new EventSpec { Id = "OnListAvailableProducts", Method = "OnListAvailableProducts", Shape = EventShape.Override, DisplayName = "On List Available Products", Description = "The list of products in this world has arrived.", Params = new EventParam[] { new EventParam("products", ParamType.OtherType("VRC.Economy.IProduct[]")) } });
            Add(new EventSpec { Id = "OnListProductOwners", Method = "OnListProductOwners", Shape = EventShape.Override, DisplayName = "On List Product Owners", Description = "The list of players who own a product has arrived.", Params = new EventParam[] { new EventParam("product", ParamType.OtherType("VRC.Economy.IProduct")), new EventParam("owners", ParamType.OtherType("System.String[]")) } });
            Add(new EventSpec { Id = "OnVRCPlusMassGift", Method = "OnVRCPlusMassGift", Shape = EventShape.Override, DisplayName = "On VRC+ Mass Gift", Description = "A player gifts VRC+ to several players.", Params = new EventParam[] { new EventParam("gifter", PlayerT), new EventParam("numGifts", ParamType.Of(ValueKind.Int)) }, PlayerParam = "gifter" });

            Layout("Physics", "OnPlayerCollisionEnter", "OnTriggerEnter", "OnTriggerExit", "OnPlayerTriggerStay", "OnPlayerCollisionExit", "OnPlayerCollisionStay", "OnPlayerParticleCollision", "OnControllerColliderHitPlayer", "OnTriggerStay", "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay", "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D", "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D", "OnControllerColliderHit", "OnParticleCollision", "OnParticleTrigger", "OnJointBreak", "OnJointBreak2D", "OnTransformParentChanged", "OnTransformChildrenChanged");
            Layout("Avatar", "OnAvatarChanged", "OnAvatarEyeHeightChanged", "OnPlayerSuspendChanged", "OnContactEnter", "OnContactExit", "OnPhysBoneGrabbed", "OnPhysBoneReleased", "OnPhysBonePosed", "OnPhysBoneUnPosed", "OnDroneTriggerEnter", "OnDroneTriggerExit", "OnDroneTriggerStay");
            Layout("Input", "InputJump", "InputUse", "InputGrab", "InputDrop", "InputMoveHorizontal", "InputMoveVertical", "InputLookHorizontal", "InputLookVertical", "OnInputMethodChanged", "OnLanguageChanged", "MidiNoteOn", "MidiNoteOff", "MidiControlChange");
            Layout("UI", "UiButtonClick", "UiToggleChanged", "UiSliderChanged");
            Layout("Video", "OnVideoReady", "OnVideoStart", "OnVideoEnd", "OnVideoLoop", "OnVideoError", "OnVideoPlay", "OnVideoPause");
            Layout("Network", "OnOwnershipTransferred", "OnDeserialization", "OnPreSerialization", "OnPostSerialization", "OnMasterTransferred", "OnPlayerRestored", "OnPlayerDataUpdated", "OnPlayerDataStorageWarning", "OnPlayerDataStorageExceeded", "OnPlayerObjectStorageWarning", "OnPlayerObjectStorageExceeded", "OnPersistenceUsageUpdated", "OnSpawn");
            Layout("Frame", "Update", "LateUpdate", "FixedUpdate", "PostLateUpdate", "OnBecameVisible", "OnBecameInvisible", "OnWillRenderObject", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderObject", "OnAnimatorMove", "OnAnimatorIK", "OnDestroy", "OnAsyncGpuReadbackComplete", "OnVRCCameraSettingsChanged", "OnVRCQualitySettingsChanged", "OnScreenUpdate");
            Layout("Load", "OnStringLoadSuccess", "OnStringLoadError", "OnImageLoadSuccess", "OnImageLoadError");
            Layout("Economy", "OnPurchaseConfirmed", "OnPurchaseConfirmedMultiple", "OnPurchaseExpired", "OnProductEvent", "OnPurchasesLoaded", "OnListPurchases", "OnListAvailableProducts", "OnListProductOwners", "OnVRCPlusMassGift");
            Add(new EventSpec
            {
                Id = ScriptNotifiedId, Shape = EventShape.Listen, DisplayName = "Notified By Another Script",
                Description = "Another U# script (a video player, a pen...) calls this trigger back by name. Tripwire registers with it at Start.",
            });
            Layout("Link", "ScriptNotified");
            Layout("Advanced", "OnEnable", "OnDisable");

            foreach (var id in new[] { "OnVideoReady", "OnVideoStart", "OnVideoEnd", "OnVideoLoop", "OnVideoError", "OnVideoPlay", "OnVideoPause" })
                byId[id].NeedsVideoPlayerOnSameObject = true;
            foreach (var id in new[]
                     {
                         "Update", "LateUpdate", "FixedUpdate", "PostLateUpdate", "OnTriggerStay", "OnTriggerStay2D", "OnCollisionStay", "OnCollisionStay2D",
                         "OnPlayerTriggerStay", "OnPlayerCollisionStay", "OnDroneTriggerStay", "OnWillRenderObject", "OnPreCull", "OnPreRender", "OnPostRender",
                         "OnRenderObject", "OnAnimatorMove", "OnAnimatorIK", "InputMoveHorizontal", "InputMoveVertical", "InputLookHorizontal", "InputLookVertical",
                     })
                byId[id].Frequent = true;
        }
    }
}
