using System.Collections.Generic;
using UnityEngine;

namespace Tripwire
{
    /// <summary>
    /// Display names of players, for the cards that only some people may use ("Who can use it": in the list / not in it).
    /// Several triggers can share one list. Only the editor reads it: the names are written into the generated code.
    /// </summary>
    [CreateAssetMenu(menuName = "Tripwire/Player List", fileName = "Player List")]
    public class TripwirePlayerList : ScriptableObject
    {
        [Tooltip("One VRChat display name per entry, exactly as shown in VRChat.")]
        public List<string> names = new List<string>();
    }
}
