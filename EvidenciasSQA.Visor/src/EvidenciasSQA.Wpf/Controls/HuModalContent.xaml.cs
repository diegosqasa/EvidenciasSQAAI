using System.Windows;
using System.Windows.Controls;

namespace EvidenciasSQA.Wpf.Controls
{
    /// <summary>
    /// Contenido del modal para ingreso de datos de Historia de Usuario (HU).
    /// Se usa dentro de BaseModal. Expone propiedades HuId y HuName para el resultado.
    /// </summary>
    public partial class HuModalContent : UserControl
    {
        public HuModalContent()
        {
            InitializeComponent();
            Loaded += (_, _) => IdHuTextBox.Focus();
        }

        public string HuId => IdHuTextBox?.Text?.Trim() ?? "";

        public string HuName => NombreHuTextBox?.Text?.Trim() ?? "";

        public bool Validate()
        {
            bool valid = true;

            if (string.IsNullOrWhiteSpace(HuId))
            {
                IdHuErrorText.Visibility = Visibility.Visible;
                valid = false;
            }
            else
            {
                IdHuErrorText.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrWhiteSpace(HuName))
            {
                NombreHuErrorText.Visibility = Visibility.Visible;
                valid = false;
            }
            else
            {
                NombreHuErrorText.Visibility = Visibility.Collapsed;
            }

            return valid;
        }

        public void ClearErrors()
        {
            IdHuErrorText.Visibility = Visibility.Collapsed;
            NombreHuErrorText.Visibility = Visibility.Collapsed;
        }
    }
}