using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace RtsSandbox.Input;

public sealed class CameraController
{
    private IWindow _window = null!;

    public Vector3 Position;
    public float Yaw = -90f;
    public float Pitch = -30f;

    public Matrix4x4 View { get; private set; }
    public Matrix4x4 Proj { get; private set; }

    private Vector2 _lastMouse;
    private bool _firstMouse = true;

    // zoom as camera distance along forward (simple)
    private float _moveSpeed = 40f;
    private float _mouseSensitivity = 0.15f;

    public void Init(IWindow window)
    {
        _window = window;
        Position = new Vector3(128f, 60f, 320f);
        RecalcMatrices();
    }

    public void Update(IKeyboard? kb, double dt)
    {
        if (kb == null) { RecalcMatrices(); return; }

        var forward = GetForward();
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

        float s = _moveSpeed * (float)dt;

        if (kb.IsKeyPressed(Key.W)) Position += forward * s;
        if (kb.IsKeyPressed(Key.S)) Position -= forward * s;
        if (kb.IsKeyPressed(Key.D)) Position += right * s;
        if (kb.IsKeyPressed(Key.A)) Position -= right * s;
        if (kb.IsKeyPressed(Key.Space)) Position += Vector3.UnitY * s;
        if (kb.IsKeyPressed(Key.ShiftLeft)) Position -= Vector3.UnitY * s;

        RecalcMatrices();
    }

    public void OnMouseMove(IMouse mouse, Vector2 pos)
    {
        // Rotate: Middle mouse drag
        if (mouse.IsButtonPressed(MouseButton.Middle))
        {
            if (_firstMouse) { _lastMouse = pos; _firstMouse = false; return; }
            var delta = pos - _lastMouse;
            _lastMouse = pos;

            Yaw += delta.X * _mouseSensitivity;
            Pitch -= delta.Y * _mouseSensitivity;
            Pitch = Math.Clamp(Pitch, -89f, 89f);
            return;
        }

        // Pan: Alt + Left drag (handled in Picking with terrain hit delta later)
        // Here we just reset rotate state
        _firstMouse = true;
    }

    public void OnScroll(float deltaY)
    {
        // Zoom: move along forward (clamp Y a bit too)
        var f = GetForward();
        Position += f * (deltaY * 8f);
        Position.Y = Math.Clamp(Position.Y, 5f, 500f);
        RecalcMatrices();
    }

    private void RecalcMatrices()
    {
        float aspect = _window.Size.X / (float)_window.Size.Y;
        Proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspect, 0.1f, 2000f);

        var f = GetForward();
        View = Matrix4x4.CreateLookAt(Position, Position + f, Vector3.UnitY);
    }

    public Vector3 GetForward()
    {
        float yaw = MathF.PI / 180f * Yaw;
        float pitch = MathF.PI / 180f * Pitch;

        var f = new Vector3(
            MathF.Cos(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Sin(yaw) * MathF.Cos(pitch));

        return Vector3.Normalize(f);
    }
}
