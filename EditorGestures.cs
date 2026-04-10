using Avalonia.Input;

namespace DagEdit
{
    internal static class EditorGestures
    {
        public static KeyGesture Delete { get; set; } = new(Key.Delete);

        public static KeyGesture Undo { get; set; } = new(Key.Z, KeyModifiers.Control);

        public static KeyGesture Redo { get; set; } = new(Key.Y, KeyModifiers.Control);

        public static KeyGesture RedoAlt { get; set; } = new(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
    }
}
