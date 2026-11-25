using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(TilemapCollider2D), typeof(CompositeCollider2D), typeof(PlatformEffector2D))]
public sealed class OneWayPlatformPaths : MonoBehaviour
{
    private void Awake()
    {
        var composite = GetComponent<CompositeCollider2D>();
        var effector = GetComponent<PlatformEffector2D>();
        var tilemap = GetComponent<TilemapCollider2D>();
        if (!tilemap.enabled || !composite.usedByEffector || !effector.enabled || !effector.useOneWay ||
            composite.geometryType != CompositeCollider2D.GeometryType.Outlines || composite.pathCount < 2)
            return;

        // The tilemap still authors the outlines. Each runtime collider can be ignored separately.
        var shapes = new PhysicsShapeGroup2D();
        int shapeCount = composite.shapeCount;
        for (int i = 0; i < shapeCount; i++)
        {
            shapes.Clear();
            composite.GetShapes(shapes, i, 1);
            var platform = gameObject.AddComponent<CustomCollider2D>();
            platform.sharedMaterial = composite.sharedMaterial;
            platform.isTrigger = composite.isTrigger;
            platform.layerOverridePriority = composite.layerOverridePriority;
            platform.includeLayers = composite.includeLayers;
            platform.excludeLayers = composite.excludeLayers;
            platform.forceSendLayers = composite.forceSendLayers;
            platform.forceReceiveLayers = composite.forceReceiveLayers;
            platform.contactCaptureLayers = composite.contactCaptureLayers;
            platform.callbackLayers = composite.callbackLayers;
            platform.usedByEffector = true;
            platform.SetCustomShapes(shapes);
        }
        // Disabling the tilemap source removes the original combined physics shapes.
        tilemap.enabled = false;
    }
}
