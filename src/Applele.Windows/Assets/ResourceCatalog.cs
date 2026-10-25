using Applele.Core;

namespace Applele.Windows.Assets;

internal sealed class ResourceCatalog : IDisposable
{
    readonly Dictionary<string, SpriteAsset> sprites = new(StringComparer.OrdinalIgnoreCase);
    readonly string root;
    public PetDefinition Definition { get; }
    public ResourceCatalog(string directory)
    {
        root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        Definition = PetDefinition.Load(Path.Combine(root, "config", "pet.json"));
        try
        {
            foreach (var spec in Definition.Forms.SelectMany(x => x.Reactions.Prepend(x.Idle)))
            {
                if (sprites.ContainsKey(spec.File)) continue;
                var path=Resolve(spec);
                sprites.Add(spec.File, new SpriteAsset(path));
            }
            foreach(var frameSet in Definition.Forms.Select(x=>x.Frames).OfType<CharacterFrameSet>())
            {
                // Validate declarations eagerly, decode atlas textures only for the active form.
                Resolve(frameSet.Actions.Texture);Resolve(frameSet.Walk.Texture);
                if(frameSet.WalkContinuation!=null)Resolve(frameSet.WalkContinuation.Texture);
            }
        }
        catch { Dispose(); throw; }
    }
    public SpriteAsset Get(SpriteDefinition spec) => sprites[spec.File];
    public string Resolve(SpriteDefinition spec)
    {
        var path=Path.GetFullPath(Path.Combine(root,spec.File));
        if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("素材路径越出项目目录。");
        if(!File.Exists(path))throw new FileNotFoundException("缺少正式透明素材，无法启动："+spec.File,path);
        return path;
    }
    public void Dispose() { foreach (var sprite in sprites.Values) sprite.Dispose(); sprites.Clear(); }
}
