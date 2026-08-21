using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EvidenciasSQA.Wpf.Controls
{
    /// <summary>
    /// Componente único para el botón de cierre (X) en todos los modales.
    /// Encapsula: posición fija en esquina superior derecha, centrado vertical en header,
    /// tamaño uniforme, forma circular, hover naranja corporativo, cursor pointer,
    /// focus visible, aria-label="Cerrar", soporte teclado (Tab, Enter, Space).
    /// Prohibido implementar botones de cierre personalizados fuera de este componente.
    /// </summary>
    public partial class ModalCloseButton : UserControl
    {
        public static readonly RoutedEvent CloseRequestedEvent =
            EventManager.RegisterRoutedEvent(nameof(CloseRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModalCloseButton));

        public static readonly DependencyProperty CloseCommandProperty =
            DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(ModalCloseButton), new PropertyMetadata(null));

        public event RoutedEventHandler CloseRequested
        {
            add => AddHandler(CloseRequestedEvent, value);
            remove => RemoveHandler(CloseRequestedEvent, value);
        }

        public ICommand CloseCommand
        {
            get => (ICommand)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public ModalCloseButton()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
            CloseCommand?.Execute(null);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            // Asegurar que el botón interno tenga el focus visual correcto
            if (CloseButton != null)
            {
                CloseButton.GotFocus += (_, _) => VisualStateManager.GoToState(CloseButton, "Focused", true);
                CloseButton.LostFocus += (_, _) => VisualStateManager.GoToState(CloseButton, "Unfocused", true);
            }
        }
    }
}