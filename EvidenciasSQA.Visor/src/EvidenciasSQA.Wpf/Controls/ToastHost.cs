using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using EvidenciasSQA.Core.Services;

namespace EvidenciasSQA.Wpf.Controls;

/// <summary>
/// Toast in-app (paridad con #toast de la web): fondo #003060, borde izquierdo de 5px
/// coloreado por tipo, icono emoji, texto blanco 600, esquinas redondeadas y sombra.
/// Entrada: deslizamiento hacia abajo + fade in (0.4 s) con pop del icono (0.35 s).
/// Salida: deslizamiento hacia arriba + fade out (0.3 s). Reemplazo único: un nuevo
/// mensaje sustituye al anterior (spec web, ui-utils.js showToast).
/// </summary>
public sealed class ToastHost : Border
{
    private readonly TextBlock _icon;
    private readonly TextBlock _text;
    private readonly TranslateTransform _transform;
    private readonly ScaleTransform _iconScale;
    private readonly DispatcherTimer _hideTimer;

    public ToastHost()
    {
        AutomationProperties.SetAutomationId(this, "ToastHost");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Background = new SolidColorBrush(Color.FromRgb(0x00, 0x30, 0x60));
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(5, 0, 0, 0);
        Padding = new Thickness(20, 12, 20, 12);
        MinWidth = 320;
        MaxWidth = 520;
        MaxHeight = 100;

        // Sombra (box-shadow 0 4px 15px rgba(0,0,0,0.25))
        Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 15,
            ShadowDepth = 4,
            Opacity = 0.25
        };

        // Grid interno: icono + texto (flex con gap 12px de la web)
        var grid = new Grid { Margin = new Thickness(0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI"),
            FontSize = 22,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _iconScale = new ScaleTransform(1, 1)
        };
        Grid.SetColumn(_icon, 0);

        _text = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 440
        };
        Grid.SetColumn(_text, 1);

        grid.Children.Add(_icon);
        grid.Children.Add(_text);
        Child = grid;

        // Desplazamiento inicial fuera de vista (la web parte de translateY(-100px))
        _transform = new TranslateTransform(0, -60);
        RenderTransform = _transform;
        Opacity = 0;
        Visibility = Visibility.Collapsed;

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideTimer.Tick += (_, _) => Hide();
    }

    /// <summary>Muestra un mensaje, sustituyendo al toast anterior si estuviera visible.</summary>
    public void Show(ToastMessage message)
    {
        (Brush border, string icon, Brush iconColor) = MapType(message.Type);
        BorderBrush = border;
        _icon.Text = icon;
        _icon.Foreground = iconColor;
        _text.Text = message.Text;

        // Reiniciar estado y cancelar el ocultado pendiente
        _hideTimer.Stop();
        Visibility = Visibility.Visible;
        AutomationProperties.SetName(this, message.Text);

        // Icono pop (toastPop 0.35s: 0% scale(0) → 60% scale(1.3) → 100% scale(1))
        _iconScale.ScaleX = 0;
        _iconScale.ScaleY = 0;
        var pop = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        _iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

        // Entrada: slide down + fade in (0.4s)
        var show = new Storyboard();
        var yIn = new DoubleAnimation(-60, 0, TimeSpan.FromMilliseconds(400)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(yIn, this);
        Storyboard.SetTargetProperty(yIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        var oIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
        Storyboard.SetTarget(oIn, this);
        Storyboard.SetTargetProperty(oIn, new PropertyPath(OpacityProperty));
        show.Children.Add(yIn);
        show.Children.Add(oIn);
        show.Begin(this, true);

        _hideTimer.Interval = message.Duration ?? TimeSpan.FromSeconds(3);
        _hideTimer.Start();
    }

    /// <summary>Oculta el toast con la animación de salida (slide up + fade out).</summary>
    private void Hide()
    {
        _hideTimer.Stop();
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        var hide = new Storyboard();
        var yOut = new DoubleAnimation(0, -60, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(yOut, this);
        Storyboard.SetTargetProperty(yOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        var oOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        Storyboard.SetTarget(oOut, this);
        Storyboard.SetTargetProperty(oOut, new PropertyPath(OpacityProperty));
        hide.Children.Add(yOut);
        hide.Children.Add(oOut);
        hide.Completed += (_, _) => Visibility = Visibility.Collapsed;
        hide.Begin(this, true);
    }

    /// <summary>
    /// Mapea el tipo de toast a borde + icono + color de icono de contraste
    /// (main.css #toast + ui-utils.js; el icono usa un tono brillante del acento
    /// para distinguirse del fondo azul oscuro #003060).
    /// </summary>
    private static (Brush Border, string Icon, Brush IconColor) MapType(ToastType type) => type switch
    {
        ToastType.Error => (new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)), "\u274C", new SolidColorBrush(Color.FromRgb(0xff, 0x80, 0x80))),
        ToastType.Warning => (new SolidColorBrush(Color.FromRgb(0xf5, 0x9e, 0x0b)), "\u26A0\uFE0F", new SolidColorBrush(Color.FromRgb(0xfb, 0xbf, 0x24))),
        ToastType.Info => (new SolidColorBrush(Color.FromRgb(0x3b, 0x82, 0xf6)), "\u2139\uFE0F", new SolidColorBrush(Color.FromRgb(0x60, 0xa5, 0xfa))),
        _ => (new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)), "\u2705", new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80)))
    };
}