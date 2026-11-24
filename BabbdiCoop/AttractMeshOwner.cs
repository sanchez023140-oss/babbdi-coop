using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>v0.10.33: each cabinet has its own desynced flipbook mesh; destroy it together
    /// with the screen GameObject (level changes rebuild the district).</summary>
    internal class AttractMeshOwner : MonoBehaviour
    {
        public Mesh Mesh;

        void OnDestroy()
        {
            if (Mesh != null) Destroy(Mesh);
        }
    }
}
