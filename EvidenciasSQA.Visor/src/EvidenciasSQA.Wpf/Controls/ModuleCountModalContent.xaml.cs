using System.Windows;
using System.Windows.Controls;

namespace EvidenciasSQA.Wpf.Controls
{
    /// <summary>
    /// Contenido del modal para selección de cantidad de casos de prueba (1-20).
    /// Se usa dentro de BaseModal. Expone propiedad ModuleCount para el resultado.
    /// </summary>
    public partial class ModuleCountModalContent : UserControl
    {
        public ModuleCountModalContent()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                CountTextBox.Focus();
                CountTextBox.SelectAll();
            };
        }

        public int ModuleCount { get; private set; } = 2;

        public bool Validate()
        {
            if (!int.TryParse(CountTextBox?.Text?.Trim(), out int total) || total < 1 || total > 20)
            {
                CountErrorText.Visibility = Visibility.Visible;
                return false;
            }

            ModuleCount = total;
            CountErrorText.Visibility = Visibility.Collapsed;
            return true;
        }
    }
}