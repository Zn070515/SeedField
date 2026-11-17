using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Crowd LOD batched by texture: one MultiMesh draw command per distinct sprite
/// texture instead of one textured quad per entity. It only reads snapshots and
/// never touches WorldState.
/// </summary>
public sealed class LodBatchRenderer
{
    private readonly VisualAssetCatalog _catalog;
    private readonly QuadMesh _quad = new() { Size = Vector2.One };
    private readonly Dictionary<Texture2D, MultiMesh> _meshes = new();
    private readonly Dictionary<Texture2D, List<Instance>> _groups = new();

    public LodBatchRenderer(VisualAssetCatalog catalog)
    {
        _catalog = catalog;
    }

    public void Draw(
        CanvasItem canvas,
        IReadOnlyList<VisualEntitySnapshot> entities,
        Func<Vec2, Vector2> map,
        Func<long, int> facing,
        Rect2 viewport)
    {
        foreach (var group in _groups.Values)
        {
            group.Clear();
        }

        foreach (var entity in entities)
        {
            var position = map(entity.Position);
            if (!viewport.HasPoint(position))
            {
                continue;
            }

            var frame = _catalog.Get(entity.VisualAssetName, facing(entity.EntityId));
            if (frame is null)
            {
                continue;
            }

            var size = frame.Footprint * EntityVisual.ScreenScale;
            var center = position + size *
                (new Vector2(0.5f, 0.5f) - frame.Pivot);
            if (!_groups.TryGetValue(frame.Diffuse, out var group))
            {
                group = new List<Instance>();
                _groups.Add(frame.Diffuse, group);
            }

            group.Add(new Instance(center, size));
        }

        foreach (var pair in _groups)
        {
            if (pair.Value.Count == 0)
            {
                continue;
            }

            if (!_meshes.TryGetValue(pair.Key, out var mesh))
            {
                mesh = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
                    Mesh = _quad,
                };
                _meshes.Add(pair.Key, mesh);
            }

            mesh.InstanceCount = pair.Value.Count;
            for (var index = 0; index < pair.Value.Count; index++)
            {
                var instance = pair.Value[index];
                mesh.SetInstanceTransform2D(
                    index,
                    new Transform2D(
                        0,
                        new Vector2(instance.Size, instance.Size),
                        0,
                        instance.Center));
            }

            canvas.DrawMultimesh(mesh, pair.Key);
        }
    }

    private readonly record struct Instance(Vector2 Center, float Size);
}
