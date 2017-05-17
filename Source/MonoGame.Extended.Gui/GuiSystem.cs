using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Gui.Controls;
using MonoGame.Extended.Input.InputListeners;
using MonoGame.Extended.ViewportAdapters;

namespace MonoGame.Extended.Gui
{
    public interface IGuiContext
    {
        BitmapFont DefaultFont { get; }
        Vector2 CursorPosition { get; }
    }

    public class GuiSystem : IGuiContext, IRectangular
    {
	    private readonly ViewportAdapter _viewportAdapter;
        private readonly IGuiRenderer _renderer;

        private GuiControl _preFocusedControl;
        private GuiControl _focusedControl;
        private GuiControl _hoveredControl;
	    private MouseListener _mouseListener;
	    private TouchListener _touchListener;
	    private KeyboardListener _keyboardListener;

	    public GuiSystem(ViewportAdapter viewportAdapter, IGuiRenderer renderer)
        {
            _viewportAdapter = viewportAdapter;
            _renderer = renderer;

            MouseListener = new MouseListener(viewportAdapter);
            TouchListener = new TouchListener(viewportAdapter);
            KeyboardListener = new KeyboardListener();

            Screens = new GuiScreenCollection(this)
            {
                OnItemAdded = screen => screen.Layout(this, _viewportAdapter.BoundingRectangle)
            };
        }

        public GuiScreenCollection Screens { get; }

        public GuiScreen ActiveScreen => Screens.LastOrDefault();

        public Rectangle BoundingRectangle => _viewportAdapter.BoundingRectangle;

        public Vector2 CursorPosition { get; set; }

        public BitmapFont DefaultFont => ActiveScreen?.Skin?.DefaultFont;

		public MouseListener MouseListener
	    {
		    get { return _mouseListener; }
			set {
				_mouseListener = value; 
				BindMouseListener(_mouseListener);
			}
	    }

	    public TouchListener TouchListener
	    {
		    get { return _touchListener; }
		    set {
			    _touchListener = value;
			    BindTouchListener(_touchListener);
		    }
	    }

	    public KeyboardListener KeyboardListener
	    {
		    get { return _keyboardListener; }
		    set {
			    _keyboardListener = value;
			    BindKeyboardListener(_keyboardListener);
		    }
	    }

	    public void Update(GameTime gameTime)
        {
            TouchListener.Update(gameTime);
            MouseListener.Update(gameTime);
            KeyboardListener.Update(gameTime);
        }

        public void Draw(GameTime gameTime)
        {
            var deltaSeconds = gameTime.GetElapsedSeconds();

            _renderer.Begin();

            foreach (var screen in Screens)
            {
                if (screen.IsVisible)
                {
                    DrawChildren(screen.Controls, deltaSeconds);
                    DrawWindows(screen.Windows, deltaSeconds);
                }
            }

            var cursor = ActiveScreen.Skin?.Cursor;

            if (cursor != null)
                _renderer.DrawRegion(cursor.TextureRegion, CursorPosition, cursor.Color);

            _renderer.End();
        }

	    private void BindMouseListener(MouseListener mouseListener)
	    {
		    mouseListener.MouseMoved += (s, e) => OnPointerMoved(GuiPointerEventArgs.FromMouseArgs(e));
            mouseListener.MouseDown += (s, e) => OnPointerDown(GuiPointerEventArgs.FromMouseArgs(e));
            mouseListener.MouseUp += (s, e) => OnPointerUp(GuiPointerEventArgs.FromMouseArgs(e));
            mouseListener.MouseWheelMoved += (s, e) => _focusedControl?.OnScrolled(e.ScrollWheelDelta);
	    }

	    private void BindTouchListener(TouchListener touchListener)
	    {
            touchListener.TouchStarted += (s, e) => OnPointerDown(GuiPointerEventArgs.FromTouchArgs(e));
            touchListener.TouchMoved += (s, e) => OnPointerMoved(GuiPointerEventArgs.FromTouchArgs(e));
            touchListener.TouchEnded += (s, e) => OnPointerUp(GuiPointerEventArgs.FromTouchArgs(e));
	    }

		private void BindKeyboardListener(KeyboardListener keyboardListener)
	    {
            keyboardListener.KeyTyped += (sender, args) => _focusedControl?.OnKeyTyped(this, args);
            keyboardListener.KeyPressed += (sender, args) => _focusedControl?.OnKeyPressed(this, args);
	    }

        private void DrawWindows(GuiWindowCollection windows, float deltaSeconds)
        {
            foreach (var window in windows)
            {
                window.Draw(this, _renderer, deltaSeconds);
                DrawChildren(window.Controls, deltaSeconds);
            }
        }

        private void DrawChildren(GuiControlCollection controls, float deltaSeconds)
        {
            foreach (var control in controls.Where(c => c.IsVisible))
                control.Draw(this, _renderer, deltaSeconds);

            foreach (var childControl in controls.Where(c => c.IsVisible))
                DrawChildren(childControl.Controls, deltaSeconds);
        }

        private void OnPointerDown(GuiPointerEventArgs args)
        {
            if (ActiveScreen == null || !ActiveScreen.IsVisible)
                return;

            _preFocusedControl = FindControlAtPoint(args.Position);
            _hoveredControl?.OnPointerDown(this, args);
        }

        private void OnPointerUp(GuiPointerEventArgs args)
        {
            if (ActiveScreen == null || !ActiveScreen.IsVisible)
                return;

            var postFocusedControl = FindControlAtPoint(args.Position);

            if (_preFocusedControl == postFocusedControl)
            {
                var focusedControl = postFocusedControl;

                if (_focusedControl != focusedControl)
                {
                    if (_focusedControl != null)
                        _focusedControl.IsFocused = false;

                    _focusedControl = focusedControl;

                    if (_focusedControl != null)
                        _focusedControl.IsFocused = true;
                }
            }

            _preFocusedControl = null;
            _hoveredControl?.OnPointerUp(this, args);
        }

        private void OnPointerMoved(GuiPointerEventArgs args)
        {
            CursorPosition = args.Position.ToVector2();

            if (ActiveScreen == null || !ActiveScreen.IsVisible)
                return;

            var hoveredControl = FindControlAtPoint(args.Position);

            if (_hoveredControl != hoveredControl)
            {
                _hoveredControl?.OnPointerLeave(this, args);
                _hoveredControl = hoveredControl;
                _hoveredControl?.OnPointerEnter(this, args);
            }
        }

        private GuiControl FindControlAtPoint(Point point)
        {
            if (ActiveScreen == null || !ActiveScreen.IsVisible)
                return null;

            //for(var i = Windows.Count - 1; i >= 0; i--)
            //{
            //    var window = Windows[i];
            //    var control = FindControlAtPoint(window.Controls, point);

            //    if (control != null)
            //        return control;
            //}

            return FindControlAtPoint(ActiveScreen.Controls, point);
        }

        private static GuiControl FindControlAtPoint(GuiControlCollection controls, Point point)
        {
            var topMostControl = (GuiControl) null;

            for (var i = controls.Count - 1; i >= 0; i--)
            {
                var control = controls[i];

                if (control.IsVisible)
                {
                    if (topMostControl == null && control.BoundingRectangle.Contains(point))
                        topMostControl = control;

                    if (control.Controls.Any())
                    {
                        var child = FindControlAtPoint(control.Controls, point);

                        if (child != null)
                            topMostControl = child;
                    }
                }
            }

            return topMostControl;
        }
    }
}