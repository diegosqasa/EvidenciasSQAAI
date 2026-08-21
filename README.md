# EvidenciasSQA

Aplicación de escritorio para captura, edición, gestión y exportación de evidencias SQA (Software Quality Assurance). Desarrollada en .NET 9 + WPF + WinForms, con extensión de navegador para captura directa desde Chrome/Firefox.

## Arquitectura

```
src/
├── EvidenciasSQA/              ← App principal (WPF + WinForms tray)
│   ├── Controls/               ← Renderers personalizados (TrayMenuStyle, AcrylicRenderer)
│   ├── Forms/                  ← MainForm (tray), SettingsForm, CaptureForm, AboutForm
│   ├── HttpListeners/          ← Listener HTTP local para capturas desde extensión
│   ├── Media/                  ← Iconos y recursos embebidos
│   └── App.xaml.cs             ← Entry point WPF
├── EvidenciasSQA.Base/         ← Librería compartida (core, helpers, interfaces)
├── EvidenciasSQA.Editor/       ← Editor de imágenes (WPF)
├── EvidenciasSQA.Visor/        ← Visor de capturas (WPF)
│   ├── EvidenciasSQA.Core/     ← Lógica compartida del visor
│   ├── EvidenciasSQA.Wpf/      ← UI del visor
│   └── EvidenciasSQA.Editor.Wpf/ ← Editor integrado
├── EvidenciasSQA.BuildTasks/   ← Tareas MSBuild personalizadas
├── Ext_Web/                    ← Extensiones de navegador (Chrome/Firefox)
└── Media/                      ← Recursos compartidos (iconos SQA1.png, SQA_ICO.ico)
```

## Stack Tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 9 (Windows 10.0.19041+) |
| UI Visor/Editor | WPF |
| UI Tray | WinForms (`ContextMenuStrip`) |
| Editor de imágenes | Custom WPF canvas |
| Extensión navegador | Chrome MV3 / Firefox (Manifest V3) |
| HTTP Listener | `System.Net.HttpListener` (localhost) |
| Captura de pantalla | DXGI Desktop Duplication + GDI+ |
| Idiomas | i18n vía resource files |

## Requisitos

- Windows 10/11 (x64)
- .NET 9 SDK (para compilar)
- Chrome o Firefox (para la extensión de navegador)

## Compilación

```bash
cd src
dotnet restore
dotnet build --configuration Release
```

El ejecutable se genera en:
```
src/EvidenciasSQA/bin/Release/net9.0-windows10.0.19041.0/
```

## Módulos Principales

### Tray Menu (System Tray)
Menú contextual premium con estilo Windows 11:
- Fondo acrílico translúcido con cantos redondeados
- Paleta oscura con acentos corporativos (naranja `#FF6B00`)
- Íconos vectoriales lineales personalizados
- Header con logo SQA y tipografía Segoe UI Variable
- Atajos de teclado alineados a la derecha

**Opciones del menú:**
| Acción | Atajo |
|---|---|
| Capturar región | `PrintScreen` |
| Capturar ventana | `Alt + PrintScreen` |
| Capturar pantalla completa | `Ctrl + PrintScreen` |
| Capturar ventana de lista | — |
| Abrir Visor | — |
| Salir | — |

### Captura de Pantalla
- Región interactiva (selección con mouse)
- Ventana específica (selección por clic)
- Pantalla completa (todos los monitores)
- Captura desde extensión de navegador

### Editor de Imágenes
- Recorte, rotación, escalado
- Dibujo libre, texto, formas
- Resaltado y desenfoque
- Deshacer/Rehacer

### Visor de Capturas
- Grid de thumbnails con scroll virtual
- Vista previa a tamaño completo
- Exportación individual o masiva a Word
- Organización por sesiones y fechas

### Extensión de Navegador
- Captura visible de la pestaña actual
- Selección de área de captura
- Sincronización automática con la app de escritorio
- Soporte offline con reintento

## Estructura de Datos

| Dato | Ubicación |
|---|---|
| Capturas | `~/Capturas_QA/` |
| Metadatos | SQLite (`~/Capturas_QA/sqa_metadata.db`) |
| Configuración | `%APPDATA%/EvidenciasSQA/` |
| Logs | `~/Capturas_QA/logs/` |

## Iconos del Proyecto

| Archivo | Uso |
|---|---|
| `Media/SQA1.png` | Logo principal (header menú, about) |
| `Media/SQA1.ico` | Icono de la aplicación y system tray |
| `Media/SQA_ICO.ico` | Icono alternativo |
| `Media/SQA_ICO.png` | Logo alternativo |

## Licencia

GNU General Public License v1.0 o posterior. Ver [LICENSE](LICENSE) para detalles.
