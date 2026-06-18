using System;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DagEdit
{
    internal class MultiGesture
    {
        private readonly object[] _gestures;
        private readonly Match _match;

        public MultiGesture(Match match, params object[] gestures)
        {
            _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
            _match = match;
        }

        internal enum Match
        {
            Any,
            All,
        }

        public bool Matches(object targetElement, RoutedEventArgs eventArgs)
        {
            var pointerEventArgs = eventArgs as PointerEventArgs;
            var keyEventArgs = eventArgs as KeyEventArgs;

            if (_match == Match.Any)
            {
                foreach (object gesture in _gestures)
                {
                    if ((gesture is PointerGesture pointerGesture && pointerEventArgs != null &&
                         pointerGesture.Matches(targetElement, pointerEventArgs)) ||
                        (gesture is KeyGesture keyGesture && keyEventArgs != null && keyGesture.Matches(keyEventArgs)))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                foreach (object gesture in _gestures)
                {
                    if ((gesture is PointerGesture pointerGesture && (pointerEventArgs == null ||
                                                                      !pointerGesture.Matches(
                                                                          targetElement,
                                                                          pointerEventArgs))) ||
                        (gesture is KeyGesture keyGesture &&
                         (keyEventArgs == null || !keyGesture.Matches(keyEventArgs))))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
