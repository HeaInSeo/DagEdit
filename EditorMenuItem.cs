using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace DagEdit
{
    // TODO menuitem 을 그대로 받아서 일단은 axaml 은 생략했다. 차후 수정할때 생각해야 한다.
    internal class EditorMenuItem : MenuItem
    {
        static EditorMenuItem()
        {
            BackgroundProperty.OverrideDefaultValue<EditorMenuItem>(Brushes.Brown);
        }

        protected override Type StyleKeyOverride => typeof(MenuItem);
    }
}
