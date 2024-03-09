using System.Numerics;
using Raylib_cs;
using ImGuiNET;
using rlImGui_cs;
using RainEd.Tiles;

namespace RainEd;

class TileEditor : IEditorMode
{
    public string Name { get => "Tiles"; }

    private readonly EditorWindow window;
    private Tile? selectedTile;
    private int selectedMaterialIdx = 0;
    private bool isToolActive = false;

    private int selectedGroup = 0;
    private string searchQuery = "";
    
    private class AvailableGroup
    {
        public int GroupIndex;
        public List<Tile> Tiles;

        public AvailableGroup(int groupIndex, List<Tile> tiles)
        {
            GroupIndex = groupIndex;
            Tiles = tiles;
        }
    }

    // available groups (available = passes search)
    private readonly List<int> availableGroups = new();

    private int materialBrushSize = 1;

    public TileEditor(EditorWindow window) {
        this.window = window;
        selectedTile = null;
    }

    public void Load()
    {
        isToolActive = false;
        ProcessSearch();
    }

    public void Unload()
    {
        window.CellChangeRecorder.TryPushChange();
    }

    /*
    COLLAPSING HEADER TILE SELECTOR UI

    public void DrawToolbar() {
        if (ImGui.Begin("Tile Selector", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            // work layer
            {
                int workLayerV = window.WorkLayer + 1;
                ImGui.SetNextItemWidth(ImGui.GetTextLineHeightWithSpacing() * 4f);
                ImGui.InputInt("Work Layer", ref workLayerV);
                window.WorkLayer = Math.Clamp(workLayerV, 1, 3) - 1;
            }

            // default material dropdown
            ImGui.Text("Default Material");
            int defaultMat = (int) window.Editor.Level.DefaultMaterial - 1;
            ImGui.Combo("##DefaultMaterial", ref defaultMat, Level.MaterialNames, Level.MaterialNames.Length, 999999);
            window.Editor.Level.DefaultMaterial = (Material) defaultMat + 1;
            
            bool? headerOpenState = null;

            if (ImGui.Button("Collapse All"))
                headerOpenState = false;
            
            ImGui.SameLine();
            if (ImGui.Button("Expand All"))
                headerOpenState = true;

            ImGui.SameLine();
            var right = ImGui.GetCursorPosX();
            ImGui.NewLine();

            ImGui.SetNextItemWidth(right - ImGui.GetCursorPosX() - ImGui.GetStyle().ItemSpacing.X);
            ImGui.InputTextWithHint("##search", "Search...", ref searchQuery, 64, ImGuiInputTextFlags.AlwaysOverwrite);
            var searchQueryL = searchQuery.ToLower();

            // the tiles in the group that pass search test
            var tilesInGroup = new List<Tiles.TileData>();
            var materialsInGroup = new List<int>();
            
            if (ImGui.BeginChild("List", ImGui.GetContentRegionAvail()))
            {
                // get list of materials that match search
                for (int i = 0; i < Level.MaterialNames.Length; i++)
                {
                    var name = Level.MaterialNames[i];
                    if (searchQuery.Length == 0 || name.ToLower().Contains(searchQueryL))
                        materialsInGroup.Add(i);
                }

                // materials section
                if (materialsInGroup.Count > 0 && ImGui.CollapsingHeader("Materials"))
                {
                    foreach (int i in materialsInGroup)
                    {
                        var name = Level.MaterialNames[i];
                        var isSelected = selectedTile == null && selectedMaterialIdx == i;
                        if (ImGui.Selectable(name, isSelected))
                        {
                            selectedTile = null;
                            selectedMaterialIdx = i;
                        }
                    }
                }

                foreach (var group in window.Editor.TileDatabase.Categories)
                {
                    bool groupNameInQuery = searchQuery.Length == 0 || group.Name.ToLower().Contains(searchQueryL);

                    // get a list of the tiles that are in query
                    tilesInGroup.Clear();
                    foreach (Tiles.TileData tile in group.Tiles)
                    {
                        if (searchQuery.Length == 0 || tile.Name.ToLower().Contains(searchQueryL))
                        {
                            tilesInGroup.Add(tile);
                        }
                    }

                    if (headerOpenState is not null) ImGui.SetNextItemOpen(headerOpenState.GetValueOrDefault());
                    if ((groupNameInQuery || tilesInGroup.Count > 0) && ImGui.CollapsingHeader(group.Name))
                    {
                        foreach (var tile in tilesInGroup)
                        {
                            if (ImGui.Selectable(tile.Name, selectedTile is not null && selectedTile == tile))
                            {
                                selectedTile = tile;
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                rlImGui.Image(tile.PreviewTexture);
                                ImGui.EndTooltip();
                            }
                        }
                    }
                }
            } ImGui.EndChild();
        }
    }
    */

    private void ProcessSearch()
    {
        var tileDb = window.Editor.TileDatabase;
        availableGroups.Clear();
        
        var queryLower = searchQuery.ToLower();

        // check if any materials have any entries that pass the search query
        for (int i = 0; i < Level.MaterialNames.Length; i++)
        {
            if (searchQuery.Length == 0 || Level.MaterialNames[i].ToLower().Contains(queryLower))
            {
                availableGroups.Add(-1); // -1 means materials, i guess
                break;
            }
        }

        // find groups that have any entries that pass the search query
        for (int i = 0; i < tileDb.Categories.Count; i++)
        {
            // if search query is empty, add this group to the search query
            if (searchQuery == "")
            {
                availableGroups.Add(i);
                continue;
            }

            // search is not empty, so scan the tiles in this group
            // if there is one tile that that passes the search query, the
            // group gets put in the list
            // (further searching is done in DrawViewport)
            for (int j = 0; j < tileDb.Categories[i].Tiles.Count; j++)
            {
                // this tile passes the search, so add this group to the search results
                if (tileDb.Categories[i].Tiles[j].Name.ToLower().Contains(queryLower))
                {
                    availableGroups.Add(i);
                    break;
                }
            }
        }
    }

    public void DrawToolbar()
    {
        var tileDb = window.Editor.TileDatabase;
        
        if (ImGui.Begin("Tile Selector", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            // work layer
            {
                int workLayerV = window.WorkLayer + 1;
                ImGui.SetNextItemWidth(ImGui.GetTextLineHeightWithSpacing() * 4f);
                ImGui.InputInt("Work Layer", ref workLayerV);
                window.WorkLayer = Math.Clamp(workLayerV, 1, 3) - 1;
            }

            // default material button (or press E)
            int defaultMat = (int) window.Editor.Level.DefaultMaterial - 1;
            ImGui.TextUnformatted($"Default Material: {Level.MaterialNames[defaultMat]}");

            if (selectedTile != null)
                ImGui.BeginDisabled();
            
            if ((ImGui.Button("Set Selected Material as Default") || EditorWindow.IsKeyPressed(ImGuiKey.Q)) && selectedTile == null)
            {
                var oldMat = window.Editor.Level.DefaultMaterial;
                var newMat = (Material)(selectedMaterialIdx + 1);
                window.Editor.Level.DefaultMaterial = newMat;

                if (oldMat != newMat)
                    RainEd.Instance.ChangeHistory.Push(new ChangeHistory.DefaultMaterialChangeRecord(oldMat, newMat));
            }

            if (ImGui.IsItemHovered() && selectedTile != null)
            {
                ImGui.SetTooltip("A material is not selected");
            }

            if (selectedTile != null)
                ImGui.EndDisabled();

            // search bar
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var searchInputFlags = ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EscapeClearsAll;
            if (ImGui.InputTextWithHint("##Search", "Search...", ref searchQuery, 128, searchInputFlags))
            {
                ProcessSearch(); // find the groups which have at least one tile that passes the search query
            }

            var queryLower = searchQuery.ToLower();

            // if there is only one available group, automatically select it
            if (availableGroups.Count == 1) selectedGroup = availableGroups[0];

            // group list box
            var halfWidth = ImGui.GetContentRegionAvail().X / 2f - ImGui.GetStyle().ItemSpacing.X / 2f;
            var boxHeight = ImGui.GetContentRegionAvail().Y;

            if (ImGui.BeginListBox("##Groups", new Vector2(halfWidth, boxHeight)))
            {
                foreach (int i in availableGroups)
                {
                    // material group
                    if (i == -1)
                    {
                        if (ImGui.Selectable("Materials", i == selectedGroup))
                            selectedGroup = -1;
                    }

                    // tile group
                    else if (ImGui.Selectable(tileDb.Categories[i].Name, i == selectedGroup))
                        selectedGroup = i;
                }
                
                ImGui.EndListBox();
            }
            
            // group listing (effects) list box
            ImGui.SameLine();
            if (ImGui.BeginListBox("##Tiles", new Vector2(halfWidth, boxHeight)))
            {
                // material group
                if (selectedGroup == -1)
                {
                    for (int i = 0; i < Level.MaterialNames.Length; i++)
                    {
                        // first, check if name passes search query
                        var name = Level.MaterialNames[i];
                        if (searchQuery == "" || name.ToLower().Contains(queryLower))
                        {
                            // material selection
                            var isSelected = selectedTile == null && selectedMaterialIdx == i;
                            if (ImGui.Selectable(name, isSelected))
                            {
                                selectedTile = null;
                                selectedMaterialIdx = i;
                            }
                        }
                    }
                }

                // tile group
                else
                {
                    var tileList = tileDb.Categories[selectedGroup].Tiles;

                    for (int i = 0; i < tileList.Count; i++)
                    {
                        var tile = tileList[i];
                        if (searchQuery != "" && !tile.Name.ToLower().Contains(queryLower)) continue;

                        if (ImGui.Selectable(tile.Name, selectedTile == tile))
                        {
                            selectedTile = tile;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            rlImGui.Image(tile.PreviewTexture);
                            ImGui.EndTooltip();
                        }
                    }
                }
                
                ImGui.EndListBox();
            }
        }

        // tab to change work layer
        if (EditorWindow.IsTabPressed())
        {
            window.WorkLayer = (window.WorkLayer + 1) % 3;
        }

        // A and D to change selected group
        if (window.Editor.IsShortcutActivated(RainEd.ShortcutID.NavLeft))
        {
            selectedGroup--;
            if (selectedGroup < -1)
                selectedGroup = tileDb.Categories.Count - 1;
            
            // select the first tile in this group
            if (selectedGroup == -1)
            {
                selectedTile = null;
                selectedMaterialIdx = 0;
            }
            else
            {
                selectedTile = tileDb.Categories[selectedGroup].Tiles[0];
            }
        }

        if (window.Editor.IsShortcutActivated(RainEd.ShortcutID.NavRight))
        {
            selectedGroup++;
            if (selectedGroup >= tileDb.Categories.Count)
                selectedGroup = -1;
            
            // select the first tile in this group
            if (selectedGroup == -1)
            {
                selectedTile = null;
                selectedMaterialIdx = 0;
            }
            else
            {
                selectedTile = tileDb.Categories[selectedGroup].Tiles[0];
            }
        }

        // W and S to change selected tile in group
        if (window.Editor.IsShortcutActivated(RainEd.ShortcutID.NavDown)) // S
        {
            if (selectedGroup == -1)
            {
                selectedMaterialIdx = Mod(selectedMaterialIdx + 1, Level.MaterialNames.Length);
            }
            else if (selectedTile != null)
            {
                // select the next tile, or wrap around if at end of the list
                if (selectedTile.Category.Index != selectedGroup)
                {
                    selectedTile = tileDb.Categories[selectedGroup].Tiles[0];
                }
                else
                {
                    var tileList = selectedTile.Category.Tiles;
                    selectedTile = tileList[Mod(tileList.IndexOf(selectedTile) + 1, tileList.Count)];
                }
            }
        }

        if (window.Editor.IsShortcutActivated(RainEd.ShortcutID.NavUp)) // W
        {
            if (selectedGroup == -1)
            {
                selectedMaterialIdx = Mod(selectedMaterialIdx - 1, Level.MaterialNames.Length);
            }
            else if (selectedTile != null)
            {
                // select the previous tile, or wrap around if at end of the list
                if (selectedTile.Category.Index != selectedGroup)
                {
                    selectedTile = tileDb.Categories[selectedGroup].Tiles[0];
                }
                else
                {
                    var tileList = selectedTile.Category.Tiles;
                    selectedTile = tileList[Mod(tileList.IndexOf(selectedTile) - 1, tileList.Count)];
                }
            }
        }
    }

    private static int Mod(int a, int b)
        => (a%b + b)%b;

    private static void DrawTile(int tileInt, int x, int y, float lineWidth, Color color)
    {
        if (tileInt == 0)
        {
            // air is represented by a cross (OMG ASCEND WITH GORB???)
            // an empty cell (-1) would mean any tile is accepted
            Raylib.DrawLineEx(
                startPos: new Vector2(x * Level.TileSize + 5, y * Level.TileSize + 5),
                endPos: new Vector2((x+1) * Level.TileSize - 5, (y+1) * Level.TileSize - 5),
                lineWidth,
                color
            );

            Raylib.DrawLineEx(
                startPos: new Vector2((x+1) * Level.TileSize - 5, y * Level.TileSize + 5),
                endPos: new Vector2(x * Level.TileSize + 5, (y+1) * Level.TileSize - 5),
                lineWidth,
                color
            );
        }
        else if (tileInt > 0)
        {
            var cellType = (GeoType) tileInt;
            switch (cellType)
            {
                case GeoType.Solid:
                    Raylib.DrawRectangleLinesEx(
                        new Rectangle(x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize),
                        lineWidth,
                        color
                    );
                    break;
                
                case GeoType.Platform:
                    Raylib.DrawRectangleLinesEx(
                        new Rectangle(x * Level.TileSize, y * Level.TileSize, Level.TileSize, 10),
                        lineWidth,
                        color
                    );
                    break;
                
                case GeoType.Glass:
                    Raylib.DrawRectangleLinesEx(
                        new Rectangle(x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize),
                        lineWidth,
                        color
                    );
                    break;

                case GeoType.ShortcutEntrance:
                    Raylib.DrawRectangleLinesEx(
                        new Rectangle(x * Level.TileSize, y * Level.TileSize, Level.TileSize, Level.TileSize),
                        lineWidth,
                        Color.Red
                    );
                    break;

                case GeoType.SlopeLeftDown:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y+1) * Level.TileSize,
                        new Vector2(x+1, y) * Level.TileSize,
                        new Vector2(x, y) * Level.TileSize,
                        color
                    );
                    break;

                case GeoType.SlopeLeftUp:
                    Raylib.DrawTriangleLines(
                        new Vector2(x, y+1) * Level.TileSize,
                        new Vector2(x+1, y+1) * Level.TileSize,
                        new Vector2(x+1, y) * Level.TileSize,
                        color
                    );
                    break;

                case GeoType.SlopeRightDown:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y) * Level.TileSize,
                        new Vector2(x, y) * Level.TileSize,
                        new Vector2(x, y+1) * Level.TileSize,
                        color
                    );
                    break;

                case GeoType.SlopeRightUp:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y+1) * Level.TileSize,
                        new Vector2(x, y) * Level.TileSize,
                        new Vector2(x, y+1) * Level.TileSize,
                        color
                    );
                    break;
            }
        }
    }

    public void DrawViewport(RlManaged.RenderTexture2D mainFrame, RlManaged.RenderTexture2D[] layerFrames)
    {
        window.BeginLevelScissorMode();

        var wasToolActive = isToolActive;
        isToolActive = false;

        var level = window.Editor.Level;
        var levelRender = window.LevelRenderer;

        // draw level background (solid white)
        Raylib.DrawRectangle(0, 0, level.Width * Level.TileSize, level.Height * Level.TileSize, new Color(127, 127, 127, 255));

        // draw layers
        for (int l = Level.LayerCount-1; l >= 0; l--)
        {
            int offset = l * 2;

            // draw layer into framebuffer
            Raylib.BeginTextureMode(layerFrames[l]);

            Raylib.ClearBackground(new Color(0, 0, 0, 0));
            Rlgl.PushMatrix();
                Rlgl.Translatef(offset, offset, 0f);
                levelRender.RenderGeometry(l, new Color(0, 0, 0, 255));
                levelRender.RenderTiles(l, 255);
            Rlgl.PopMatrix();
        }

        // draw alpha-blended result into main frame
        Raylib.BeginTextureMode(mainFrame);
        for (int l = Level.LayerCount-1; l >= 0; l--)
        {
            Rlgl.PushMatrix();
            Rlgl.LoadIdentity();

            var alpha = l == window.WorkLayer ? 255 : 50;
            Raylib.DrawTextureRec(
                layerFrames[l].Texture,
                new Rectangle(0f, layerFrames[l].Texture.Height, layerFrames[l].Texture.Width, -layerFrames[l].Texture.Height),
                Vector2.Zero,
                new Color(255, 255, 255, alpha)
            );
            Rlgl.PopMatrix();
        }

        levelRender.RenderGrid();
        levelRender.RenderBorder();

        if (window.IsViewportHovered)
        {
            var modifyGeometry = EditorWindow.IsKeyDown(ImGuiKey.G);
            var forcePlace = EditorWindow.IsKeyDown(ImGuiKey.F);

            // begin change if left or right button is down
            // regardless of what it's doing
            if (window.IsMouseDown(ImGuiMouseButton.Left) || window.IsMouseDown(ImGuiMouseButton.Right))
            {
                if (!wasToolActive) window.CellChangeRecorder.BeginChange();
                isToolActive = true;
            }

            // render selected tile
            if (selectedTile is not null)
            {
                // mouse position is at center of tile
                // tileOrigin is the top-left of the tile, so some math to adjust
                //var tileOriginFloat = window.MouseCellFloat + new Vector2(0.5f, 0.5f) - new Vector2(selectedTile.Width, selectedTile.Height) / 2f;
                var tileOriginX = window.MouseCx - selectedTile.CenterX;
                int tileOriginY = window.MouseCy - selectedTile.CenterY;

                // draw tile requirements
                // second layer
                if (selectedTile.HasSecondLayer)
                {
                    for (int x = 0; x < selectedTile.Width; x++)
                    {
                        for (int y = 0; y < selectedTile.Height; y++)
                        {
                            Rlgl.PushMatrix();
                            Rlgl.Translatef(tileOriginX * Level.TileSize + 2, tileOriginY * Level.TileSize + 2, 0);

                            sbyte tileInt = selectedTile.Requirements2[x,y];
                            DrawTile(tileInt, x, y, 1f / window.ViewZoom, new Color(0, 255, 0, 255));
                            Rlgl.PopMatrix();
                        }
                    }
                }

                // first layer
                for (int x = 0; x < selectedTile.Width; x++)
                {
                    for (int y = 0; y < selectedTile.Height; y++)
                    {
                        Rlgl.PushMatrix();
                        Rlgl.Translatef(tileOriginX * Level.TileSize, tileOriginY * Level.TileSize, 0);

                        sbyte tileInt = selectedTile.Requirements[x,y];
                        DrawTile(tileInt, x, y, 1f / window.ViewZoom, new Color(255, 0, 0, 255));
                        Rlgl.PopMatrix();
                    }
                }

                // check if requirements are satisfied
                TilePlacementStatus validationStatus;

                if (level.IsInBounds(window.MouseCx, window.MouseCy))
                    validationStatus = ValidateTilePlacement(selectedTile, tileOriginX, tileOriginY, modifyGeometry || forcePlace);
                else
                    validationStatus = TilePlacementStatus.OutOfBounds;

                // draw tile preview
                Raylib.DrawTextureEx(
                    selectedTile.PreviewTexture,
                    new Vector2(tileOriginX, tileOriginY) * Level.TileSize - new Vector2(2, 2),
                    0,
                    (float)Level.TileSize / 16,
                    validationStatus == TilePlacementStatus.Success ? new Color(255, 255, 255, 200) : new Color(255, 0, 0, 200)
                );

                if (modifyGeometry)
                    window.StatusText = "Force Geometry";
                else if (forcePlace)
                    window.StatusText = "Force Placement";

                // place tile on click
                if (window.IsMouseDown(ImGuiMouseButton.Left))
                {
                    if (validationStatus == TilePlacementStatus.Success)
                    {
                        PlaceTile(
                            selectedTile,
                            tileOriginX, tileOriginY,
                            window.WorkLayer, window.MouseCx, window.MouseCy,
                            modifyGeometry
                        );
                    }
                    else if (window.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        string errStr = validationStatus switch {
                            TilePlacementStatus.OutOfBounds => "Tile is out of bounds",
                            TilePlacementStatus.Overlap => "Tile is overlapping another",
                            TilePlacementStatus.Geometry => "Tile geometry requirements not met",
                            _ => "Unknown tile placement error"
                        };

                        window.Editor.ShowNotification(errStr);
                    }
                }
            }

            // render selected material
            else
            {
                if (EditorWindow.IsKeyDown(ImGuiKey.ModShift))
                {
                    window.OverrideMouseWheel = true;

                    if (Raylib.GetMouseWheelMove() > 0.0f)
                        materialBrushSize += 2;
                    else if (Raylib.GetMouseWheelMove() < 0.0f)
                        materialBrushSize -= 2;
                    
                    materialBrushSize = Math.Clamp(materialBrushSize, 1, 21);
                }

                // draw grid cursor
                int cursorLeft = window.MouseCx - materialBrushSize / 2;
                int cursorTop = window.MouseCy - materialBrushSize / 2;

                Raylib.DrawRectangleLinesEx(
                    new Rectangle(
                        cursorLeft * Level.TileSize,
                        cursorTop * Level.TileSize,
                        Level.TileSize * materialBrushSize,
                        Level.TileSize * materialBrushSize
                    ),
                    1f / window.ViewZoom,
                    LevelEditRender.MaterialColors[selectedMaterialIdx]
                );

                // place material
                int placeMode = 0;
                if (window.IsMouseDown(ImGuiMouseButton.Left))
                    placeMode = 1;
                else if (window.IsMouseDown(ImGuiMouseButton.Right))
                    placeMode = 2;
                
                if (placeMode != 0)
                {
                    // place or remove materials inside cursor
                    for (int x = cursorLeft; x <= window.MouseCx + materialBrushSize / 2; x++)
                    {
                        for (int y = cursorTop; y <= window.MouseCy + materialBrushSize / 2; y++)
                        {
                            if (!level.IsInBounds(x, y)) continue;
                            ref var cell = ref level.Layers[window.WorkLayer, x, y];

                            if (cell.HasTile()) continue;

                            if (placeMode == 1)
                            {
                                cell.Material = (Material) selectedMaterialIdx + 1;
                            }
                            else
                            {
                                cell.Material = Material.None;
                            }
                        }
                    }
                }
            }

            if (window.IsMouseInLevel())
            {
                int tileLayer = window.WorkLayer;
                int tileX = window.MouseCx;
                int tileY = window.MouseCy;

                var mouseCell = level.Layers[window.WorkLayer, window.MouseCx, window.MouseCy];

                // if this is a tile body, find referenced tile head
                if (mouseCell.HasTile() && mouseCell.TileHead is null)
                {
                    tileLayer = mouseCell.TileLayer;
                    tileX = mouseCell.TileRootX;
                    tileY = mouseCell.TileRootY;
                }

                // eyedropper
                if (EditorWindow.IsKeyPressed(ImGuiKey.E))
                {
                    // tile eyedropper
                    if (mouseCell.HasTile())
                    {
                        selectedTile = level.Layers[tileLayer, tileX, tileY].TileHead;
                        
                        if (selectedTile is null)
                        {
                            throw new Exception("Could not find tile head");
                        }
                        else
                        {
                            selectedGroup = selectedTile.Category.Index;
                        }
                    }

                    // material eyedropper
                    else
                    {
                        if (mouseCell.Material > 0)
                        {
                            selectedTile = null;
                            selectedMaterialIdx = (int)mouseCell.Material - 1;
                            selectedGroup = -1;
                        }
                    }
                }

                // remove tile on right click
                if (window.IsMouseDown(ImGuiMouseButton.Right) && mouseCell.HasTile())
                {
                    RemoveTile(tileLayer, tileX, tileY, modifyGeometry);
                }
            }
        }

        if (wasToolActive && !isToolActive)
            window.CellChangeRecorder.PushChange();
        
        Raylib.EndScissorMode();
    }

    private enum TilePlacementStatus
    {
        Success,
        OutOfBounds,
        Overlap,
        Geometry
    };

    private TilePlacementStatus ValidateTilePlacement(Tiles.Tile tile, int tileLeft, int tileTop, bool force)
    {
        var level = window.Editor.Level;

        for (int x = 0; x < tile.Width; x++)
        {
            for (int y = 0; y < tile.Height; y++)
            {
                int gx = tileLeft + x;
                int gy = tileTop + y;
                var specInt = tile.Requirements[x,y];
                var spec2Int = tile.Requirements2[x,y];

                // check that there is not already a tile here
                if (level.IsInBounds(gx, gy))
                {
                    // placing it on a tile head can introduce a bugged state,
                    // soo... even when forced... no
                    ref var cellAtPos = ref level.Layers[window.WorkLayer, gx, gy];

                    if (specInt >= 0 && cellAtPos.TileHead is not null)
                        return TilePlacementStatus.Overlap;
                    
                    // check on first layer
                    var isHead = x == tile.CenterX && y == tile.CenterY;

                    if ((isHead || specInt >= 0) && !force && cellAtPos.HasTile())
                        return TilePlacementStatus.Overlap;

                    // check on second layer
                    if (window.WorkLayer < 2)
                    {
                        if (spec2Int >= 0 && !force && level.Layers[window.WorkLayer+1, gx, gy].HasTile())
                            return TilePlacementStatus.Overlap;
                    }
                }

                
                if (!force)
                {
                    // check first layer geometry
                    if (specInt == -1) continue;
                    if (level.GetClamped(window.WorkLayer, gx, gy).Geo != (GeoType) specInt)
                        return TilePlacementStatus.Geometry;

                    // check second layer geometry
                    // if we are on layer 3, there is no second layer
                    // all checks pass
                    if (window.WorkLayer == 2) continue;
                    
                    if (spec2Int == -1) continue;
                    if (level.GetClamped(window.WorkLayer+1, gx, gy).Geo != (GeoType) spec2Int)
                        return TilePlacementStatus.Geometry;
                }
            }
        }
        
        return TilePlacementStatus.Success;
    }

    private void PlaceTile(
        Tile tile,
        int tileLeft, int tileTop,
        int layer, int tileRootX, int tileRootY,
        bool placeGeometry
    )
    {
        var level = window.Editor.Level;

        for (int x = 0; x < tile.Width; x++)
        {
            for (int y = 0; y < tile.Height; y++)
            {
                int gx = tileLeft + x;
                int gy = tileTop + y;
                if (!level.IsInBounds(gx, gy)) continue;

                int specInt = tile.Requirements[x,y];
                int spec2Int = tile.Requirements2[x,y];

                if (placeGeometry)
                {
                    // place first layer    
                    if (specInt >= 0)
                    {
                        level.Layers[layer, gx, gy].Geo = (GeoType) specInt;
                        window.LevelRenderer.MarkNeedsRedraw(gx, gy, layer);
                    }

                    // place second layer
                    if (layer < 2 && spec2Int >= 0)
                    {
                        level.Layers[layer+1, gx, gy].Geo = (GeoType) spec2Int;
                        window.LevelRenderer.MarkNeedsRedraw(gx, gy, layer+1);
                    }
                }

                // tile first 
                if (specInt >= 0)
                {
                    level.Layers[layer, gx, gy].TileRootX = tileRootX;
                    level.Layers[layer, gx, gy].TileRootY = tileRootY;
                    level.Layers[layer, gx, gy].TileLayer = layer;
                }

                // tile second layer
                if (spec2Int >= 0 && layer < 2)
                {
                    level.Layers[layer+1, gx, gy].TileRootX = tileRootX;
                    level.Layers[layer+1, gx, gy].TileRootY = tileRootY;
                    level.Layers[layer+1, gx, gy].TileLayer = layer;
                }
            }
        }

        // place tile root
        level.Layers[layer, tileRootX, tileRootY].TileHead = tile;
    }

    private void RemoveTile(int layer, int tileRootX, int tileRootY, bool removeGeometry)
    {
        var level = window.Editor.Level;
        var tile = level.Layers[layer, tileRootX, tileRootY].TileHead
            ?? throw new Exception("Attempt to remove unknown tile");
        int tileLeft = tileRootX - tile.CenterX;
        int tileTop = tileRootY - tile.CenterY;

        for (int x = 0; x < tile.Width; x++)
        {
            for (int y = 0; y < tile.Height; y++)
            {
                int gx = tileLeft + x;
                int gy = tileTop + y;
                if (!level.IsInBounds(gx, gy)) continue;

                int specInt = tile.Requirements[x,y];
                int spec2Int = tile.Requirements2[x,y];
                
                // remove tile bodies
                if (specInt >= 0)
                {
                    level.Layers[layer, gx, gy].TileRootX = -1;
                    level.Layers[layer, gx, gy].TileRootY = -1;
                    level.Layers[layer, gx, gy].TileLayer = -1;
                }

                if (spec2Int >= 0 && layer < 2)
                {
                    level.Layers[layer+1, gx, gy].TileRootX = -1;
                    level.Layers[layer+1, gx, gy].TileRootY = -1;
                    level.Layers[layer+1, gx, gy].TileLayer = -1;
                }

                // remove geometry
                if (removeGeometry)
                {
                    if (specInt >= 0)
                    {
                        level.Layers[layer, gx, gy].Geo = GeoType.Air;
                        window.LevelRenderer.MarkNeedsRedraw(gx, gy, layer);
                    }

                    if (spec2Int >= 0 && layer < 2)
                    {
                        level.Layers[layer+1, gx, gy].Geo = GeoType.Air;
                        window.LevelRenderer.MarkNeedsRedraw(gx, gy, layer+1);
                    }
                }
            }
        }

        // remove tile root
        level.Layers[layer, tileRootX, tileRootY].TileHead = null;
    }
}