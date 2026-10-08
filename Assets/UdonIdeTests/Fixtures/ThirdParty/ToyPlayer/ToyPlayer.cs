using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace ToyMedia
{
    /// <summary>
    /// A small video player (a stand-in for a third-party asset, used to test the IDE).
    /// Call <see cref="Play"/> with a URL; listeners get <c>OnToyPlayerStarted</c> and <c>OnToyPlayerStopped</c>.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ToyPlayer : UdonSharpBehaviour
    {
        /// <summary>Volume from 0 (silent) to 1.</summary>
        public float volume = 0.5f;

        /// <summary>Behaviours told about playback, by SendCustomEvent.</summary>
        public UdonSharpBehaviour[] listeners;

        [UdonSynced] VRCUrl url;
        [UdonSynced] bool playing;

        /// <summary>Starts playing <paramref name="link"/> for everyone. Only the owner can start playback.</summary>
        /// <param name="link">The video URL.</param>
        public void Play(VRCUrl link)
        {
            if (!Networking.IsOwner(gameObject)) return;
            url = link;
            playing = true;
            RequestSerialization();
            Notify("OnToyPlayerStarted");
        }

        /// <summary>Stops playback for everyone.</summary>
        public void Stop()
        {
            playing = false;
            RequestSerialization();
            Notify("OnToyPlayerStopped");
        }

        /// <summary>Whether a video is playing.</summary>
        /// <returns>True while playing.</returns>
        public bool IsPlaying()
        {
            return playing;
        }

        void Notify(string eventName)
        {
            foreach (var l in listeners) if (l != null) l.SendCustomEvent(eventName);
        }
    }
}
