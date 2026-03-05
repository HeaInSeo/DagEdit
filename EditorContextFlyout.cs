using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace DagEdit
{
    public class EditorContextFlyout : MenuFlyout
    {
        #region Constructors

        static EditorContextFlyout()
        {
        }

        public EditorContextFlyout()
        {
            Initialize();
        }

        // 이렇게 사용했을때는 editor 는 null 이 아님.
        public EditorContextFlyout(DagEditor editor) : this()
        {
            var menuItem = SetupContextMenu(editor);
            this.Items.Add(menuItem);
        }

        #endregion

        #region Methods

        private void Initialize()
        {
            this.Opened += (sender, e) => { };
            this.Closed += (sender, e) => { };
        }

        private MenuItem SetupContextMenu(DagEditor editor)
        {
            DagEditor dagEditor = editor;
            // '파일(_F)' 메뉴 아이템 생성
            var fileMenuItem = new EditorMenuItem
            {
                Header = "Node(_N)"
            };

            var newMenuItem = new EditorMenuItem
            {
                Header = "Add Node(_A)",
                InputGesture = new KeyGesture(Key.N, KeyModifiers.Control),
                HotKey = new KeyGesture(Key.N, KeyModifiers.Control),
            };
            newMenuItem.Click += (sender, e) => editor.AddNode();

            var openMenuItem = new EditorMenuItem
            {
                Header = "Open(_O)",
            };

            // 메뉴 아이템들을 '파일(_F)' 메뉴에 추가
            fileMenuItem.Items.Add(newMenuItem);
            fileMenuItem.Items.Add(new Separator());
            fileMenuItem.Items.Add(openMenuItem);

            // '파일(_F)' 메뉴 아이템을 ContextMenu에 추가
            //contextMenu.Items.Add(fileMenuItem);

            return fileMenuItem;
        }

        #endregion
    }
}