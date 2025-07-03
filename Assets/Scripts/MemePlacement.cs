using UnityEngine;
using Unity.Netcode;

public class MemePlacement : NetworkBehaviour
{
    public enum PlacementMode { World, Face }
    public PlacementMode placementMode = PlacementMode.World;
    private Logger logger;

    void Start()
    {
        if (placementMode == PlacementMode.Face)
        {
            // find object in scene with name "FacePosition"
            Transform facePosition = GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor/FacePosition").transform;
            if (facePosition != null)
            {
                transform.SetParent(facePosition);
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else
            {
                logger.Log("FacePosition not found in scene.", true, nameof(MemePlacement));
            }
        }
    }
    
    public void SetLogger(Logger logger)
    {
        this.logger = logger;
    }
}
