using RtsSandbox.Graphics;
using RtsSandbox.Input;
using RtsSandbox.UI;
using RtsSandbox.World;
using RtsSandbox.Assets;
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
    private RtsSandbox.Assets.AssetManager? _assets;
    private RtsSandbox.Graphics.GpuMesh? _loadedUnitMesh;
    private RtsSandbox.Graphics.GpuMesh? _loadedObstacleMesh;



    private double _time;

    // Drag state for selection
    private bool _dragging;
    private Vector2 _dragStart, _dragEnd;

    public Game(IWindow window) => _window = window;

    public void Load()
    {
        _gl = GL.GetApi(_window);
        _assets = new RtsSandbox.Assets.AssetManager(_gl);
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
            Console.WriteLine($"LoadMap: {type} path={path} scale={heightScale}");
            _terrain.LoadHeightmap(path, heightScale);

            // optional: props/units ကို terrain အသစ်နဲ့ re-snap လုပ်ချင်ရင်
            _props.Randomize(500, _terrain);
        };



        _tools!.OnLoadModel = (path, spawnAsUnit, spawnPos) =>
        {
            try
            {
                var mesh = _assets!.LoadGlbStaticMesh(path);

                if (spawnAsUnit)
                {
                    _loadedUnitMesh?.Dispose(_gl); // dispose previous
                    _loadedUnitMesh = mesh;
                    _units.SetUnitMesh(mesh);
                    _units.SpawnAt(new Vector2(spawnPos.X, spawnPos.Y));
                }
                else
                {
                    _loadedObstacleMesh?.Dispose(_gl);
                    _loadedObstacleMesh = mesh;
                    _props.SetPropMesh(mesh);
                    _props.PlaceSingle(new Vector2(spawnPos.X, spawnPos.Y), _terrain);
                }

                Console.WriteLine($"Loaded GLB: {path}");
                Console.WriteLine($"GLB bounds min={mesh.BoundsMin} max={mesh.BoundsMax} baseOffsetY={mesh.BaseOffsetY} radius={mesh.BoundingRadius}");
                Console.WriteLine($"GL handles vao={mesh.Vao} vbo={mesh.Vbo} ebo={mesh.Ebo} indexCount={mesh.IndexCount}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadModel failed: {ex.Message}");
            }
        };

        _tools!.OnLoadObstacles = (path, count) =>
        {
            try
            {
                var mesh = _assets!.LoadGlbStaticMesh(path);
                _loadedObstacleMesh?.Dispose(_gl);
                _loadedObstacleMesh = mesh;

                _props.SetPropMesh(mesh); // ၁။ Mesh ကို အရင်ထည့်
                _props.Randomize(count, _terrain); // ၂။ ပြီးမှ Randomize ပြန်လုပ် (ဒါဆိုရင် null မဖြစ်တော့ဘူး)

                Console.WriteLine($"Loaded obstacles: {count} from {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadObstacles failed: {ex.Message}");
            }
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
                    _tools?.SetPickedPosition(new Vector2(hit.X, hit.Z));
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
                    _tools?.SetPickedPosition(new Vector2(hit.X, hit.Z));
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
        _units.Update(_terrain, _props, dt);
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
