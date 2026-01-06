using ImGuiNET;

namespace RtsSandbox.UI;

public static class UiCapture
{
    public static bool WantMouse => ImGui.GetIO().WantCaptureMouse;
    public static bool WantKeyboard => ImGui.GetIO().WantCaptureKeyboard;
}
