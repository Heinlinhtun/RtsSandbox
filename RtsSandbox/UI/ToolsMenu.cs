using System;
using ImGuiNET;

namespace RtsSandbox.UI;

public sealed class ToolsMenu
{
    public enum MapType
    {
        HeightmapPng = 0,
        ChunkedHeightmap = 1,
        MeshMap = 2
    }

    public bool Show = true;

    // UI state
    private MapType _mapType = MapType.HeightmapPng;
    private string _mapPath = "Assets/Maps/test_height.png";
    private float _heightScale = 40f;

    private string _modelPath = "Assets/Models/unit.glb";
    private bool _spawnAsUnit = true;

    // Callbacks (wire these from Program/Game)
    public Action<MapType, string, float>? OnLoadMap;
    public Action<string, bool>? OnLoadModel; // (path, spawnAsUnit)

    public void Draw()
    {
        // Main menu bar
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Load Map..."))
                {
                    OnLoadMap?.Invoke(_mapType, _mapPath, _heightScale);
                }

                if (ImGui.MenuItem("Load Model..."))
                {
                    OnLoadModel?.Invoke(_modelPath, _spawnAsUnit);
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Toggle Tools", "F10"))
                    Show = !Show;

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }

        if (!Show) return;

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(420, 260), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Dev Tools", ref Show))
        {
            ImGui.Text("Load / Control System");
            ImGui.Separator();

            // Map section
            ImGui.Text("Map");
            int mapTypeInt = (int)_mapType;
            if (ImGui.Combo("Map Type", ref mapTypeInt, "Heightmap PNG\0Chunked Heightmap\0Mesh Map (GLB/OBJ)\0"))
                _mapType = (MapType)mapTypeInt;

            ImGui.InputText("Map Path", ref _mapPath, 512);
            ImGui.SliderFloat("Height Scale", ref _heightScale, 1f, 200f);

            if (ImGui.Button("Load Map"))
                OnLoadMap?.Invoke(_mapType, _mapPath, _heightScale);

            ImGui.Separator();

            // Model section
            ImGui.Text("Model");
            ImGui.InputText("Model Path", ref _modelPath, 512);
            ImGui.Checkbox("Spawn As Unit (else Prop)", ref _spawnAsUnit);

            if (ImGui.Button("Load Model"))
                OnLoadModel?.Invoke(_modelPath, _spawnAsUnit);
        }
        ImGui.End();
    }
}
