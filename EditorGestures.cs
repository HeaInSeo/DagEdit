using Avalonia.Input;

namespace DagEdit
{
    public static class EditorGestures
    {
        public static KeyGesture Delete { get; set; } = new KeyGesture(Key.Delete);
        public static KeyGesture Undo { get; set; } = new KeyGesture(Key.Z, KeyModifiers.Control);
        public static KeyGesture Redo { get; set; } = new KeyGesture(Key.Y, KeyModifiers.Control);
        public static KeyGesture RedoAlt { get; set; } = new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
    }
}
