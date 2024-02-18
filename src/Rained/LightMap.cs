using Raylib_cs;
using System.Numerics;
namespace RainEd.Light;

struct LightBrush
{
    public string Name;
    public RlManaged.Texture2D Texture;
}

struct BrushAtom
{
    public float rotation;
    public Rectangle rect;
    public int brush;
    public bool mode;
}

class LightBrushDatabase
{
    private readonly List<LightBrush> lightBrushes;

    public List<LightBrush> Brushes { get => lightBrushes; }

    public LightBrushDatabase()
    {
        Console.WriteLine("Initializing light cast catalog...");

        lightBrushes = new List<LightBrush>();

        foreach (var fileName in File.ReadLines("assets/light/init.txt"))
        {
            // if this line is empty, skip
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            // this line is a comment, skip
            if (fileName[0] == '#') continue;
            
            // load light texture
            var tex = RlManaged.Texture2D.Load($"assets/light/{fileName.Trim()}");
            lightBrushes.Add(new LightBrush()
            {
                Name = fileName.Trim(),
                Texture = tex
            });
        }

        Console.WriteLine("Done!");
    }
}

class LightMap
{
    private RlManaged.RenderTexture2D lightmapRt;
    private int width;
    private int height;

    public LightMap(int levelWidth, int levelHeight)
    {
        width = levelWidth * 20 + 300;
        height = levelHeight * 20 + 300;

        // create light map render texture
        lightmapRt = RlManaged.RenderTexture2D.Load(width, height);
        Raylib.BeginTextureMode(lightmapRt);
        Raylib.ClearBackground(Color.Black);
        Raylib.EndTextureMode();
    }

    public LightMap(Image lightMapImage)
    {
        width = lightMapImage.Width;
        height = lightMapImage.Height;

        // get light map as a texture
        var lightmapTex = RlManaged.Texture2D.LoadFromImage(lightMapImage);

        // put into a render texture
        lightmapRt = RlManaged.RenderTexture2D.Load(width, height);
        Raylib.BeginTextureMode(lightmapRt);
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTexture(lightmapTex, 0, 0, Color.White);
        Raylib.EndTextureMode();

        lightmapTex.Dispose();
    }

    public void Resize(int newWidth, int newHeight, int dstOriginX, int dstOriginY)
    {
        // convert from cell to lightmap coordinates
        newWidth = newWidth * 20 + 300;
        newHeight = newHeight * 20 + 300;
        dstOriginX *= 20;
        dstOriginY *= 20;

        // resize light map image
        var lightMapImage = GetImage();
        Raylib.ImageResizeCanvas(
            ref lightMapImage.Ref(),
            newWidth, newHeight,
            dstOriginX, dstOriginY,
            Color.White
        );

        // get light map as a texture
        var lightmapTex = RlManaged.Texture2D.LoadFromImage(lightMapImage);

        // put into a render texture
        lightmapRt.Dispose();
        lightmapRt = RlManaged.RenderTexture2D.Load(width, height);
        Raylib.BeginTextureMode(lightmapRt);
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTexture(lightmapTex, 0, 0, Color.White);
        Raylib.EndTextureMode();

        lightMapImage.Dispose();
        lightmapTex.Dispose();

        width = newWidth;
        height = newHeight;
    }

    public void RaylibBeginTextureMode()
    {
        Raylib.BeginTextureMode(lightmapRt);
    }

    public static void DrawAtom(LightBrushDatabase database, BrushAtom atom)
    {
        var tex = database.Brushes[atom.brush].Texture;
        Raylib.DrawTexturePro(
            tex,
            new Rectangle(0, 0, tex.Width, tex.Height),
            atom.rect,
            new Vector2(atom.rect.Width, atom.rect.Height) / 2f,
            atom.rotation,
            atom.mode ? Color.Black : Color.White
        );
    }

    public RlManaged.Image GetImage()
    {
        return RlManaged.Image.LoadFromTexture(lightmapRt.Texture);
    }
}