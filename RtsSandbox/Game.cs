using RtsSandbox.Graphics;
using RtsSandbox.Input;
using RtsSandbox.UI;
using RtsSandbox.World;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Numerics;
using ImGuiNET;


namespace RtsSandbox;

public sealed class Game
{
    private readonly IWindow _window;
    private GL _gl = null!;
    private IInputContext _input = null!;
    private IMouse? _mouse;
    private IKeyboard? _kb;

    private readonly Terrain _terrain = new();
    private readonly UnitSystem _units = new();
    private readonly PropSystem _props = new();
    private readonly NavMarkerSystem _markers = new();
    private readonly CameraController _camera = new();
    private readonly DragRectOverlay _dragRect = new();
    private readonly Renderer _renderer = new();

    private ImGuiController? _imgui;
    private ToolsMenu? _tools;
    private RtsSandbox.Graphics.GpuMesh? _loadedUnitMesh;



    private double _time;

    // Drag state for selection
    private bool _dragging;
    private Vector2 _dragStart, _dragEnd;

    public Game(IWindow window) => _window = window;

    public void Load()
    {
        _gl = GL.GetApi(_window);

        _input = _window.CreateInput();
        _kb = _input.Keyboards.Count > 0 ? _input.Keyboards[0] : null;
        _mouse = _input.Mice.Count > 0 ? _input.Mice[0] : null;

        _renderer.Init(_gl);
        _terrain.Init(_gl);          // builds heightmap + mesh + normals
        _units.Init(_terrain);       // spawn units on terrain
        _props.Init(_terrain);       // spawn unselectable props on terrain
        _markers.Init();             // marker logic (no GL yet)
        _camera.Init(_window);       // default camera

        _dragRect.Init(_gl);         // GL overlay program + rect VBO

        HookInput();
        _imgui = new ImGuiController(_gl, _window, _input);
        _tools = new ToolsMenu();

        // callbacks
        _tools.OnLoadMap = (type, path, heightScale) =>
        {
            // TODO: type switch
            // ✅ အခုက Heightmap PNG ကိုပဲ အရင်လုပ် (RTS အတွက် best)
            // terrain.LoadHeightmap(path, heightScale);
            Console.WriteLine($"LoadMap: {type} path={path} scale={heightScale}");
        };

        _tools.OnLoadModel = (path, spawnAsUnit) =>
        {
            // TODO: glTF loader ထည့်တဲ့နေ့부터 ဒီထဲကနေ
            // if (spawnAsUnit) unitModels.Load(path); else propModels.Load(path);
            Console.WriteLine($"LoadModel: path={path} asUnit={spawnAsUnit}");
        };


        // GL state
        _gl.Enable(GLEnum.DepthTest);
        // Keep culling off until you want to ensure winding is consistent for all meshes.
        // _gl.Enable(GLEnum.CullFace);
        // _gl.CullFace(GLEnum.Back);
    }

    private void HookInput()
    {
        if (_mouse == null) return;

        // Mouse move: update drag + camera rotate (middle)
        _mouse.MouseMove += (_, pos) =>
        {
            if (_dragging) _dragEnd = pos;
            _camera.OnMouseMove(_mouse!, pos);
        };

        // Wheel zoom
        _mouse.Scroll += (_, wheel) =>
        {
            _camera.OnScroll(wheel.Y);
        };

        // Mouse down
        _mouse.MouseDown += (_, btn) =>
        {
            if (btn == MouseButton.Left)
            {
                _dragging = true;
                _dragStart = _mouse.Position;
                _dragEnd = _dragStart;
            }
            else if (btn == MouseButton.Right)
            {
                if (Picking.TryGetTerrainHit(_window, _mouse!, _camera, _terrain, out var hit))
                {
                    if (_units.SelectedCount > 0)
                    {
                        _units.IssueMoveFormation(hit);
                        _markers.Spawn(hit, _time);
                    }
                }
            }
        };

        // Mouse up (click vs drag select)
        _mouse.MouseUp += (_, btn) =>
        {
            if (btn != MouseButton.Left) return;
            if (!_dragging) return;

            _dragging = false;

            bool shift = _kb?.IsKeyPressed(Key.ShiftLeft) == true || _kb?.IsKeyPressed(Key.ShiftRight) == true;
            bool ctrl = _kb?.IsKeyPressed(Key.ControlLeft) == true || _kb?.IsKeyPressed(Key.ControlRight) == true;

            if (Vector2.Distance(_dragStart, _dragEnd) >= 6f)
            {
                // Box select
                _units.ApplyBoxSelection(_window, _camera, _terrain, _dragStart, _dragEnd, additive: shift, toggle: ctrl);
            }
            else
            {
                // Click select
                if (Picking.TryGetTerrainHit(_window, _mouse!, _camera, _terrain, out var hit))
                {
                    int picked = _units.PickNearestUnitXZ(hit);
                    if (!shift && !ctrl) _units.SelectSingle(picked);
                    else _units.ToggleSelect(picked);
                }
            }
        };

        // Keyboard
        if (_kb != null)
        {
            _kb.KeyDown += (_, key, _) =>
            {
                if (key == Key.Escape) _window.Close();
                if (key == Key.Backspace) _units.ClearSelection();
            };
        }
    }

    public void Update(double dt)
    {
        _time += dt;

        _camera.Update(_kb, dt); // WASD + Space/Shift + Alt-pan
        _units.Update(_terrain, dt);
        _markers.Update(_time);

        _imgui?.Update((float)dt);

        // ✅ ImGui ကို click/typing လုပ်နေချိန် camera/mouse-look မလုပ်စေချင်ရင်:
        if (UiCapture.WantMouse || UiCapture.WantKeyboard)
            return;

    }

    public void Render(double dt)
    {
        _renderer.BeginFrame(_window, skyR: 0.53f, skyG: 0.81f, skyB: 0.98f);

        // 3D pass
        _renderer.DrawTerrainLit(_window, _camera, _terrain);
        _renderer.DrawProps(_window, _camera, _terrain, _props);
        _renderer.DrawUnits(_window, _camera, _terrain, _units);

        // 2D pass (drag rect)
        if (_dragging)
            _dragRect.Draw(_window, _dragStart, _dragEnd);

        // (Optional) marker rendering will be in Renderer (billboard ring)
        _renderer.DrawMarkers(_window, _camera, _terrain, _markers, _time);
        _tools?.Draw();
        _imgui?.Render();
    }

    public void Closing()
    {
        _dragRect.Dispose(_gl);
        _renderer.Dispose(_gl);
        _terrain.Dispose(_gl);
    }
}
