using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EvidenciasSQA.Wpf.Controls
{
    /// <summary>
    /// Componente base reutilizable para todos los modales de la aplicación.
    /// Encapsula: header estándar, ModalCloseButton, body, footer, comportamiento
    /// de apertura/cierre, accesibilidad, responsive, tamaños configurables.
    /// </summary>
    public enum ModalSize
    {
        Sm,   // 320px
        Md,   // 420px
        Lg,   // 520px
        Xl,   // 640px
        Full  // 900px
    }

    public partial class BaseModal : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(BaseModal), new PropertyMetadata("", OnTitleChanged));

        public static readonly DependencyProperty HeaderIconProperty =
            DependencyProperty.Register(nameof(HeaderIcon), typeof(object), typeof(BaseModal), new PropertyMetadata(null, OnHeaderIconChanged));

        public static readonly DependencyProperty BodyContentProperty =
            DependencyProperty.Register(nameof(BodyContent), typeof(object), typeof(BaseModal), new PropertyMetadata(null, OnBodyContentChanged));

        public static readonly DependencyProperty FooterContentProperty =
            DependencyProperty.Register(nameof(FooterContent), typeof(object), typeof(BaseModal), new PropertyMetadata(null, OnFooterContentChanged));

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(ModalSize), typeof(BaseModal), new PropertyMetadata(ModalSize.Md, OnSizeChanged));

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(BaseModal), new PropertyMetadata(false, OnIsOpenChanged));

        public static readonly DependencyProperty DialogResultProperty =
            DependencyProperty.Register(nameof(DialogResult), typeof(bool?), typeof(BaseModal), new PropertyMetadata(null));

        public static readonly RoutedEvent ClosedEvent =
            EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BaseModal));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object HeaderIcon
        {
            get => GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        public object BodyContent
        {
            get => GetValue(BodyContentProperty);
            set => SetValue(BodyContentProperty, value);
        }

        public object FooterContent
        {
            get => GetValue(FooterContentProperty);
            set => SetValue(FooterContentProperty, value);
        }

        public ModalSize Size
        {
            get => (ModalSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public bool? DialogResult
        {
            get => (bool?)GetValue(DialogResultProperty);
            set => SetValue(DialogResultProperty, value);
        }

        public event RoutedEventHandler Closed
        {
            add => AddHandler(ClosedEvent, value);
            remove => RemoveHandler(ClosedEvent, value);
        }

        private UIElement _previouslyFocusedElement;
        private bool _isAnimating;

        public BaseModal()
        {
            InitializeComponent();
            Loaded += BaseModal_Loaded;
            KeyDown += BaseModal_KeyDown;
        }

        private void BaseModal_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySize();
            UpdateFooterVisibility();
        }

        private void BaseModal_KeyDown(object sender, KeyEventArgs e)
        {
            if (!IsOpen) return;

            if (e.Key == Key.Escape)
            {
                Close(false);
                e.Handled = true;
            }
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseModal modal && modal.TitleTextElement != null)
            {
                modal.TitleTextElement.Text = e.NewValue as string ?? "";
            }
        }

        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseModal modal && modal.HeaderIconElement != null)
            {
                modal.HeaderIconElement.Content = e.NewValue;
                modal.HeaderIconElement.Visibility = e.NewValue != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnBodyContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseModal modal && modal.BodyContentElement != null)
            {
                modal.BodyContentElement.Content = e.NewValue;
            }
        }

        private static void OnFooterContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseModal modal && modal.FooterActionsElement != null)
            {
                modal.FooterActionsElement.Children.Clear();
                if (e.NewValue is UIElement element)
                {
                    modal.FooterActionsElement.Children.Add(element);
                }
                modal.UpdateFooterVisibility();
            }
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseModal modal)
            {
                modal.ApplySize();
            }
        }

        /// <summary>Log detallado del estado del modal para diagnóstico.</summary>
        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[BaseModal] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        private void LogState(string prefix = "")
        {
            Log($"{prefix} STATE: IsOpen={IsOpen}, RootGrid.Vis={RootGrid.Visibility}, RootGrid.Opac={RootGrid.Opacity}, ModalBorder.Vis={ModalBorder.Visibility}, ModalBorder.Opac={ModalBorder.Opacity}, Overlay.Vis={OverlayBackground.Visibility}, ModalBorder.OpacAnim={ModalBorder.Opacity}, Animating={_isAnimating}");
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseModal modal)
            {
                modal.Log($"OnIsOpenChanged: Old={e.OldValue}, New={e.NewValue}, IsAnimating={modal._isAnimating}");
                modal.LogState("  OnIsOpenChanged");
                if ((bool)e.NewValue)
                    modal.Open();
                else
                    modal.Close((bool?)modal.DialogResult);
            }
        }

        private void ApplySize()
        {
            if (ModalBorder == null) return;

            double width = Size switch
            {
                ModalSize.Sm => (double)FindResource("ModalWidthSm"),
                ModalSize.Md => (double)FindResource("ModalWidthMd"),
                ModalSize.Lg => (double)FindResource("ModalWidthLg"),
                ModalSize.Xl => (double)FindResource("ModalWidthXl"),
                ModalSize.Full => (double)FindResource("ModalWidthFull"),
                _ => (double)FindResource("ModalWidthMd")
            };

            ModalBorder.Width = width;
            ModalBorder.MaxWidth = width;
        }

        private void UpdateFooterVisibility()
        {
            if (FooterBorderElement != null)
            {
                FooterBorderElement.Visibility = FooterContent != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void Open()
        {
            if (_isAnimating) return;

            Log("=== OPEN REQUESTED ===");
            LogState("  Before Open");

            // Guardar elemento con foco previo
            _previouslyFocusedElement = Keyboard.FocusedElement as UIElement;

            // Mostrar el contenedor raíz (overlay + modal)
            RootGrid.Visibility = Visibility.Visible;
            LogState("  After RootGrid.Visibility=Visible");
            Focus();

            // Mover foco al primer elemento enfocable del modal
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                var firstFocusable = FindFirstFocusableElement(BodyContentElement);
                if (firstFocusable != null)
                {
                    firstFocusable.Focus();
                }
                else
                {
                    CloseButtonElement?.Focus();
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // Trapar foco dentro del modal
            GotKeyboardFocus += OnGotKeyboardFocus;
            LogState("  After Open completed");
        }

        public void Close(bool? result = null)
        {
            if (_isAnimating) return;

            Log("=== CLOSE REQUESTED ===");
            LogState("  Before Close");

            DialogResult = result;
            _isAnimating = true;

            // Animación de salida
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
            var scaleOut = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(120));

            fadeOut.Completed += (_, _) =>
            {
                Log("FadeOut COMPLETED");
                LogState("  After FadeOut Completed");
                RootGrid.Visibility = Visibility.Collapsed;
                LogState("  After RootGrid.Visibility=Collapsed");
                _isAnimating = false;
                GotKeyboardFocus -= OnGotKeyboardFocus;

                // NO llamar SetCurrentValue(IsOpenProperty, false) aquí:
                // El llamador original (quien puso IsOpen=false) ya estableció el valor.
                // Hacerlo aquí dispararía OnIsOpenChanged de nuevo → re-entrada en Close().
                // Log($"IsOpenProperty ya es false (establecido por el llamador)");

                // Restaurar foco al elemento anterior
                _previouslyFocusedElement?.Focus();

                RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
                LogState("  After Closed event raised");
                Log("Closed event raised");
            };

            Log("FadeOut STARTED");
            LogState("  Before FadeOut Started");
            ModalBorder.BeginAnimation(OpacityProperty, fadeOut);
            ModalBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            ModalBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Trapar foco: si el foco sale del modal, devolverlo al primer elemento del modal
            if (IsOpen && e.NewFocus != null)
            {
                if (!IsDescendantOfModal(e.NewFocus as DependencyObject))
                {
                    var firstFocusable = FindFirstFocusableElement(this);
                    firstFocusable?.Focus();
                    e.Handled = true;
                }
            }
        }

        private bool IsDescendantOfModal(DependencyObject element)
        {
            while (element != null)
            {
                if (element == ModalBorder) return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private UIElement FindFirstFocusableElement(DependencyObject parent)
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is UIElement uiElement && uiElement.Focusable && uiElement.IsVisible && uiElement.IsEnabled)
                {
                    return uiElement;
                }

                var result = FindFirstFocusableElement(child);
                if (result != null) return result;
            }
            return null;
        }

        private void CloseButton_CloseRequested(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}