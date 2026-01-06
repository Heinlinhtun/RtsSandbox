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
    private System.Numerics.Vector2 _spawnPos = new(120, 120);
    private System.Numerics.Vector2 _lastPickedPos = new(120, 120);
    private bool _hasPicked;

    private string _obstaclePath = "Assets/Models/unit.glb";
    private int _obstacleCount = 50;

    // Callbacks (wire these from Program/Game)
    public Action<MapType, string, float>? OnLoadMap;
    public Action<string, bool, System.Numerics.Vector2>? OnLoadModel; // (path, spawnAsUnit, spawnPos)
    public Action<string, int>? OnLoadObstacles; // (path, count)

    public void SetPickedPosition(System.Numerics.Vector2 xz)
    {
        _lastPickedPos = xz;
        _spawnPos = xz;
        _hasPicked = true;
    }

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
                    OnLoadModel?.Invoke(_modelPath, _spawnAsUnit, _spawnPos);
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

            ImGui.InputFloat2("Spawn XZ", ref _spawnPos);
            if (_hasPicked && ImGui.Button("Use Last Mouse Hit"))
                _spawnPos = _lastPickedPos;

            if (ImGui.Button("Load Model"))
                OnLoadModel?.Invoke(_modelPath, _spawnAsUnit, _spawnPos);

            ImGui.Separator();

            ImGui.Text("Obstacles");
            ImGui.InputText("Obstacle GLB", ref _obstaclePath, 512);
            ImGui.InputInt("Obstacle Count", ref _obstacleCount);
            if (ImGui.Button("Randomize Obstacles"))
                OnLoadObstacles?.Invoke(_obstaclePath, Math.Max(0, _obstacleCount));
        }
        ImGui.End();
    }
}
