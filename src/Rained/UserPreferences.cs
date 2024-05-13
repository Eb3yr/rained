using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;
using ImGuiNET;
namespace RainEd;

struct HexColor(byte r = 0, byte g = 0, byte b = 0)
{
    public byte R = r;
    public byte G = g;
    public byte B = b;

    public HexColor(string hexString) : this(0, 0, 0)
    {
        if (hexString[0] != '#')
            throw new Exception("Hex string does not begin with a #");
        
        int color = int.Parse(hexString[1..], System.Globalization.NumberStyles.HexNumber);

        R = (byte)((color >> 16) & 0xFF);
        G = (byte)((color >> 8) & 0xFF);
        B = (byte)(color & 0xFF);
    }

    public readonly override string ToString()
    {
        int combined = (R << 16) | (G << 8) | B;
        return "#" + combined.ToString("X6");
    }

    public readonly Color ToRGBA(byte alpha)
    {
        return new Color(R, G, B, alpha);
    }
}

class UserPreferences
{
    public string DataPath { get; set; }

    public bool ViewGrid { get; set; }
    public bool ViewObscuredBeams { get; set; }
    public bool ViewKeyboardShortcuts { get; set; }
    public bool ViewTileHeads { get; set; }
    public bool ViewCameras { get; set; }

    public string GeometryViewMode { get; set; }
    public string PropSnap { get; set; }

    //public bool ResizeShowScreenSize { get; set; } // whoops, i set this to false - but now i want it true by default.
    public bool HideScreenSize { get; set; }

    public enum CameraBorderModeOption : int
    {
        Standard,
        Widescreen,
        Both
    };
    public CameraBorderModeOption CameraBorderMode;

    [JsonPropertyName("cameraBorderMode")]
    public string CameraBorderModeString {
        get => CameraBorderMode switch
        {
            CameraBorderModeOption.Standard => "standardBorder",
            CameraBorderModeOption.Widescreen => "widescreenBorder",
            CameraBorderModeOption.Both => "both",
            _ => throw new Exception("Invalid CameraBorderModeOption")
        };

        set
        {
            switch(value)
            {
                case "standardBorder":
                    CameraBorderMode = CameraBorderModeOption.Standard;
                    break;

                case "widescreenBorder":
                    CameraBorderMode = CameraBorderModeOption.Widescreen;
                    break;

                case "both":
                    CameraBorderMode = CameraBorderModeOption.Both;
                    break;

                default:
                    if (RainEd.Instance is not null)
                        RainEd.Logger.Error("Invalid CameraBorderMode '{Value}'", value);
                    
                    CameraBorderMode = CameraBorderModeOption.Both;
                    break;
            }
        }
    }

    public bool WindowMaximized { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }

    public bool StaticDrizzleLingoRuntime { get; set; }
    public bool ShowRenderPreview { get; set; }
    public bool CheckForUpdates { get; set; }

    public enum AutotileMouseModeOptions
    {
        Click, Hold
    }
    public AutotileMouseModeOptions AutotileMouseMode;

    [JsonPropertyName("autotileMouseMode")]
    public string AutotileMouseModeString {
        get => AutotileMouseMode switch
            {
                AutotileMouseModeOptions.Click => "click",
                AutotileMouseModeOptions.Hold => "hold",
                _ => throw new Exception("Invalid AutotileMouseMode option")
            };
        set
        {
            switch (value)
            {
                case "click":
                    AutotileMouseMode = AutotileMouseModeOptions.Click;
                    break;
                
                case "hold":
                    AutotileMouseMode = AutotileMouseModeOptions.Hold;
                    break;
                
                default:
                    if (RainEd.Instance is not null)
                        RainEd.Logger.Error("Invalid CameraBorderMode '{value}'", value);
                    
                    AutotileMouseMode = AutotileMouseModeOptions.Hold;
                    break;
            }
        }
    }

    public HexColor LayerColor1;
    public HexColor LayerColor2;
    public HexColor LayerColor3;
    public HexColor BackgroundColor;
    
    [JsonPropertyName("layerColor1")]
    public string LayerColor1String { get => LayerColor1.ToString(); set => LayerColor1 = new HexColor(value); }
    [JsonPropertyName("layerColor2")]
    public string LayerColor2String { get => LayerColor2.ToString(); set => LayerColor2 = new HexColor(value); }
    [JsonPropertyName("layerColor3")]
    public string LayerColor3String { get => LayerColor3.ToString(); set => LayerColor3 = new HexColor(value); }
    [JsonPropertyName("bgColor")]
    public string BackgroundColorString { get => BackgroundColor.ToString(); set => BackgroundColor = new HexColor(value); }

    public string Theme { get; set; }
    
    public Dictionary<string, string> Shortcuts { get; set; }
    public List<string> RecentFiles { get; set; }

    // default user preferences
    public UserPreferences()
    {
        DataPath = Path.Combine(Boot.AppDataPath, "Data");
        
        ViewGrid = true;
        ViewObscuredBeams = false;
        ViewKeyboardShortcuts = true;
        ViewTileHeads = false;
        ViewCameras = false;

        GeometryViewMode = "overlay";
        PropSnap = "0.5x";
        HideScreenSize = false;
        CameraBorderMode = CameraBorderModeOption.Both;

        WindowMaximized = false;
        WindowWidth = Boot.DefaultWindowWidth;
        WindowHeight = Boot.DefaultWindowHeight;

        StaticDrizzleLingoRuntime = false;
        ShowRenderPreview = true;
        CheckForUpdates = true;
        AutotileMouseMode = AutotileMouseModeOptions.Hold;

        Theme = "Dark";
        LayerColor1 = new HexColor("#000000");
        LayerColor2 = new HexColor("#59ff59");
        LayerColor3 = new HexColor("#ff1e1e");
        BackgroundColor = new HexColor(127, 127, 127);

        RecentFiles = [];

        // initialize shortcuts
        Shortcuts = null!;

        if (RainEd.Instance is not null)
            SaveKeyboardShortcuts();
    }

    public static void SaveToFile(UserPreferences prefs, string filePath)
    {
        // pascal case is a superstition
        prefs.SaveKeyboardShortcuts();
        var serializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // why does it escape '+'
            WriteIndented = true
        };

        var jsonString = JsonSerializer.Serialize(prefs, serializeOptions);
        File.WriteAllText(filePath, jsonString);
    }

    public static UserPreferences LoadFromFile(string filePath)
    {
        // PASCAL CASE IS A SOCIAL CONSTRUCT
        var serializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // why does it escape '+'?
            WriteIndented = true
        };

        var jsonString = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<UserPreferences>(jsonString, serializeOptions)!;
    }

    public void LoadKeyboardShortcuts()
    {
        foreach ((string key, string shortcut) in Shortcuts)
        {
            // PASCAL CASE IS A NAZI INVENTION
            var enumName = char.ToUpperInvariant(key[0]) + key[1..];
            KeyShortcut enumShortcut = Enum.Parse<KeyShortcut>(enumName);

            KeyShortcuts.Rebind(enumShortcut, shortcut);
        }
    }

    public void SaveKeyboardShortcuts()
    {
        Shortcuts = [];
        for (int i = 0; i < (int) KeyShortcut.COUNT; i++)
        {
            var shortcut = (KeyShortcut)i;

            // PASCAL CASE WAS CREATED BY COMMUNISTS
            var srcString = shortcut.ToString();
            var key = char.ToLowerInvariant(srcString[0]) + srcString[1..];

            Shortcuts[key] = KeyShortcuts.GetShortcutString(shortcut);
        }
    }

    public void ApplyTheme()
    {
        try
        {
            var filePath = Path.Combine(Boot.AppDataPath, "config", "themes", Theme + ".json");
            var style = SerializableStyle.FromFile(filePath);
            style!.Apply(ImGui.GetStyle());
        }
        catch (Exception e)
        {
            RainEd.Logger.Error("Could not apply theme!\n{Error}", e);
            RainEd.Instance.ShowNotification("Could not apply theme");
        }
    }
}