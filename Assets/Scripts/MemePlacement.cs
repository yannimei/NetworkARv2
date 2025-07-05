using UnityEngine;
using Unity.Netcode;

public class MemePlacement : NetworkBehaviour
{
    public enum PlacementMode { World, Face }
    public PlacementMode placementMode = PlacementMode.World;

    public void AttachToAnchor(Transform anchorTransform)
    {
        if (anchorTransform != null)
        {
            transform.SetParent(anchorTransform, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }

    public void DetachFromAnchor()
    {
        transform.SetParent(null, false);
    }

    public Transform AttachToFaceAnchor()
    {
        var faceAnchor = GameObject.FindGameObjectWithTag("Face");
        if (faceAnchor != null)
        {
            AttachToAnchor(faceAnchor.transform);
        }
        return faceAnchor != null ? faceAnchor.transform : null;
    }
}
