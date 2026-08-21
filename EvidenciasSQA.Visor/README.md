# EvidenciasSQA Viewer / Editor — Módulos C# (prototipo)

Reimplementación ligera en C#/.NET 9 (WPF) de la arquitectura del proyecto EvidenciasSQA
original, adaptada al flujo de **Evidencias SQA**: captura con **Header Corporativo
SQA** horneado y visualización a 60+ FPS, con edición **delegada a un proceso
separado**.

## Arquitectura (2 ejecutables + 1 Core)

```
┌─────────────────────┐   captura (pantalla/clipboard/archivo)
│  EvidenciasSQA.Wpf │ ─────────────────────────────────────────────┐
│  (VISOR ultraligero)  │                                             ▼
│  - CorporateHeader    │  hornea header SQA (réplica image-worker.js)│
│  - FastImageViewer    │                                             ▼
│  - pan/zoom GPU 60fps │  visualización (GPU/DirectX, retenido)      │
└──────────┬───────────┘                                               │
           │  "Editar…": temp PNG + Process.Start --file               │
           ▼                                                           │
┌─────────────────────┐   guardado in-place (Ctrl+S) ── recarga viva ─┘
│ EvidenciasSQA.    │
│  Editor.Wpf         │   SurfaceDocument + DrawableObject + IDestination
│  (módulo editor)    │   PNG / .evidenciasSqa / portapapeles
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│ EvidenciasSQA.Core │  GDI+/WPF agnóstico (FrameworkReference):
│                      │  CaptureSource, CorporateHeader, SurfaceDocument,
│                      │  DrawableObject, ImageIO (.evidenciasSqa), IDestination
└─────────────────────┘
```

- **El visor NO conoce anotaciones**: solo captura → header → pantalla.
- **El editor NO conoce el header**: solo edita la imagen ya horneada.
- **Core es compartido** y sin dependencias de UI (excepto WPF/WIC para ImageSource).

## Header Corporativo SQA (spec replicada de `image-worker.js`)

| Elemento | Valor |
|---|---|
| Degradado | horizontal `#002B55` → `#004080` |
| Franja inferior | `#FF6B00`, 4 px |
| Logo | `SQA1.png`, 65×45, x=20, centrado vertical |
| Título | 18 px bold, (100, 14) |
| "Origen:" | 16 px, wrap carácter a carácter, ancho `w-130`, step 22 px |
| Meta | 17 px blanco 85 % (sin emojis: limitación GDI+) |
| Altura | `100 + (líneas-1)*22` (dinámica) |
| Detección header previo | columna x=20, de y=400 hacia arriba, fila exacta `(255,107,0)` → contenido en `y+4` |
| DPI | 96 (chunk pHYs del PNG; corregido en Fase 18, antes 300) |
| Guard | dimensión máxima 16384 |

## Formato `.evidenciasSqa`

```
[PNG fondo][XML List<DrawableObject>][Int64 longitud XML]["EvidenciasSQA01.00"]
```
Carga por marcador + longitud (XmlSerializer no tolera cola; BinaryFormatter real de
EvidenciasSQA es obsoleto en .NET 9). `RectangleDrawable` serializa `Color` vía puente ARGB
(`LineColorArgb`), porque XmlSerializer no soporta propiedades solo-lectura.

> **Compatibilidad de formato (decisión del refactoring)**: el marcador `"EvidenciasSQA01.00"`
> y la extensión `.evidenciasSqa` se **conservan intencionalmente** — son la firma binaria
> del formato compartido con el proyecto original y renombrarlos rompería archivos
> existentes. Solo los identificadores de código (namespaces, clases, comandos, enum)
> se renombraron a `EvidenciasSQA`/`evidenciasSqa`.

## Notas de memoria y rendimiento

- `WicHelper.ToImageSource` libera el HBITMAP intermedio con `DeleteObject` (siempre).
- `SurfaceDocument` es dueño del `Bitmap` de fondo y lo dispone al reemplazar.
- `BlurDrawable` cachea el blur por `(bounds, radius, BackgroundVersion)`.
- `CorporateHeader.Bake` devuelve **siempre un Bitmap nuevo** (el llamador es dueño).
- `FastImageViewer` dibuja con `DrawingContext` retenido + escala lineal: la GPU compone.
- Horneado en `Task.Run` (fuera del hilo UI); el visor mantiene el bitmap GDI+ como
  fuente de verdad y crea `ImageSource` congeladas para mostrar.

## Galería / Historial de Evidencias

Vista conmutada dentro del visor (botón **"Ver historial"** del encabezado corporativo):

- **Conmutación**: `IsHistoryViewVisible` alterna entre la galería (ItemsControl +
  WrapPanel de tarjetas) y el visor individual. Los estilos de tarjeta viven en
  `MainWindow.Resources` (ver quirk del SDK: App.xaml no puede albergarlos).
- **Fuente de datos**: `EvidenciasSQA.Core.Persistence.EvidenceRepository` — escaneo
  de `~/Capturas_QA` (sin SQLite), ordenado por fecha descendente, máx. 50 registros,
  filtro de temporales `editing_*`.
- **Tarjetas** (`EvidenceTileModel`): miniatura ligera (`WicHelper.LoadThumbnail`,
  DecodePixelWidth=320, congelada) + código, fecha, sitio y acciones
  Copiar / Descargar / Eliminar. Clic en la miniatura abre la evidencia en el visor
  sin re-hornear (la imagen ya trae header).
- **Acciones globales**: Seleccionar (marcado visual), Generar Informe (delegado a la
  app Electron SQA), Descargar Todo (OpenFolderDialog) y Eliminar todo (con confirmación).
- **Memoria**: `LoadFrozenImageSource` / `LoadThumbnail` usan `BitmapImage` con
  `CacheOption.OnLoad` + **`StreamSource = fs` obligatorio**: sin esa asignación WPF
  lanza "Se debe establecer la propiedad UriSource o StreamSource" en EndInit y la
  galería se muestra vacía (bug real encontrado y corregido en `WicHelper`).

## Modos de la galería (Selección / Informe)

Máquina de estados en `ViewerViewModel` replicando el `historial.md` de la app
Electron (estados: normal / selección simple / informe+opciones / informe+selección /
fase de módulo), con **toolbar dinámica** en el encabezado y **barras de operación**
por estado:

- **Normal** (`IsNormalMode`): Seleccionar, Generar Informe, Descargar Todo,
  Eliminar todo.
- **Selección simple** (`IsPlainSelectionMode`): contador "N seleccionadas" +
  Copiar / Descargar / Eliminar en lote + **Generar informe**.
- **Informe + opciones** (`IsInformeOptionsMode`): Completo / Seleccionado /
  Por módulos.
- **Informe + selección** (`IsInformeSelectionMode`): solo "Generar informe"
  (selección ordenada 1..N).
- **Fase de módulo** (`IsModulePhaseVisible`): "Caso de prueba X de Y" + OK/Cancelar.

Las **tarjetas** muestran badges por estado: **indicador de selección unificado**
(esquina superior izquierda — círculo transparente en modo selección que muestra
el **número de orden 1..N** sobre `#003060` al seleccionar; sin badge numérico en
la esquina inferior) y lápiz `✏️` (evidencia editada, esquina superior derecha).
El clic de tarjeta es un **Button transparente de tarjeta
completa** con `TileClickCommand` (InvokePattern por UIA: testeable y accesible);
las acciones Copiar/Descargar/Eliminar siguen en la parte inferior.

El flujo de **"Generar informe"** abre `HuModalDialog` (ID + Nombre de HU
obligatorios con errores inline) y exporta a Word; **"Por módulos"** pide el número
de casos de prueba (1-20, `ModuleCountDialog`) y avanza por fases de selección hasta
el diálogo HU y la exportación final. Tras exportar o cancelar se permanece en el
módulo Informe (replica de `hideInformeBar`/`cancelModuleExport`).

## Exportación Word (motor propio, sin NuGet)

`EvidenciasSQA.Core.Export.WordReportBuilder` genera `.docx` **manualmente** como
paquete OPC/ZIP (mismo formato que produce DocumentFormat.OpenXml), porque el
entorno de desarrollo no tiene acceso a NuGet (`dotnet add package
DocumentFormat.OpenXml` falla offline). Estructura replicada de la app Electron
(`word-export-worker.cjs` + `mergeModuleDocuments`):

- Título + tabla de HU (encabezado `002B55`/`EBF3FA`) **una sola vez**.
- Por evidencia: encabezado, tabla de metadatos (código, fecha, sitio, usuario) e
  **imagen embebida** vía `r:embed` (dimensiones leídas del header PNG/JPEG sin
  decodificar).
- `BuildDocument` (Completo/Seleccionado) y `BuildModulesDocument` (secciones por
  caso de prueba) con `IProgress<double>` para el ProgressBar del StatusBar.
- Verificación: exportación E2E produce archivos válidos abiertos por Word/OPC.
- Si más adelante hay NuGet disponible, se puede migrar a DocumentFormat.OpenXml
  sin cambiar el contrato de `WordReportModels`.

## Sistema de Diseño y Paleta Corporativa

Recursos reutilizables en `src/EvidenciasSQA.Wpf/Styles.xaml` (merged en cada ventana,
NO en App.xaml — ver quirk del SDK más abajo).

**Tema claro Fluent** (replica el diseño de la app Electron, `UI.md` de EvidenciasOK):

| Elemento | Color |
|---|---|
| Primario (header, botones pill, badges) | `#003060` |
| Primary-dark (hover) | `#001a33` |
| Fondo General | `#F3F4F6` |
| Info Bar | `#060B29` (texto 13px centrado) |
| Acento Marca (naranja) | `#FF6B00` (franja 3 px bajo el header, botón OK de fase CP) |
| Fondo Visor | `#F4F6F9` |
| Fondo Galería | `#f4f7f6` |
| Tarjeta Fondo | `#FFFFFF` |
| Texto Primario | `#FFFFFF` (sobre header) / `#111827`–`#1f2937` (tarjetas) |
| Texto Secundario | `#4b5563` / `#64748b` |
| Pill botones (transparente) | `#f8fafc` texto, hover `#1A003060` |
| Badge "editada" | `#ff9f1c` |
| Peligro / Eliminar | `#DC3545` (header) / `#ff8080` (sobre header) / `#cc0000` (errores de modal) |

Estilos definidos: `PrimaryHeaderButtonStyle`, `HeaderLinkButtonStyle`,
`HeaderDangerLinkButtonStyle`, `TileButtonStyle`, `TileDangerButtonStyle`,
`HelpButtonStyle`, `PhaseOkButtonStyle` (`#FF6B00` pill), `ConfirmCancelButtonStyle`,
`ConfirmAcceptButtonStyle` (pill `#003060`), `ModalHeaderStyle` (`#003060` +
borde inferior 3px `#FF6B00` + radius 12,12,0,0), `ModalCloseButtonStyle`,
`DialogInputStyle`, `ToolbarSeparator`.

> **Quirks del merge de diccionarios (SDK 9.0.x, verificados empíricamente)**:
> `Source` relativo se resuelve contra la carpeta del XAML que lo declara (una ventana
> en `Views\` buscaría `views/styles.xaml`) → usar URI absoluto
> `pack://application:,,,/Styles.xaml`. El elemento de propiedad
> `ResourceDictionary.MergedDictionaries` debe ir **antes** del contenido del
> diccionario (MC3088 si va intercalado), y un `<ResourceDictionary>` explícito no
> puede mezclarse con items hermanos directos de `Window.Resources` (XamlDuplicate:
> "Resources ya se ha establecido") — el `DataTemplate` de la galería vive dentro del
> diccionario explícito.
>
> **Otro quirk del layout (verificado con muestreo de píxeles)**: `Border`, `Grid` y
> `DockPanel` NO tienen `Padding` (MC3072) — envolver en un `Border` contenedor.
> Y cuidado con el índice de filas: un elemento con `Grid.Row` igual al `Count` de
> filas (fuera de rango) provoca colapso silencioso del contenido — el área quedó en
> blanco sin errores de compilación; se diagnosticó con colores de choque (Red/Green/
> Yellow) + PrintWindow.

## Diálogos y sesiones remotas (quirk conocido)

En sesiones remotas/administrativas, `ShowDialog()` **con `Owner` asignado cuelga**
sin mostrar la ventana (bug encontrado: el mismo `Window` plano funciona sin owner).
Por eso los diálogos WPF (`HuModalDialog`, `ModuleCountDialog`) se abren **sin
Owner** y el ViewModel deshabilita la ventana principal (`IsEnabled = false`) con
`finally` para restaurarla, replicando el comportamiento modal sin el cuelgue.
`SQA_TEST_SAVEPATH` (variable de entorno) omite el `SaveFileDialog` para pruebas
E2E; el diálogo normal se usa cuando la variable no está definida.

## Build

```powershell
dotnet build EvidenciasSQA.sln
```

El build del visor copia el exe del editor a su output (`CopyEditorExe` target):
la delegación por proceso localiza `EvidenciasSQA.Editor.Wpf.exe` junto al visor.

### Quirk conocido del SDK (9.0.x)

Cuando `App.xaml` no declara `StartupUri`, `PresentationBuildTasks` **no compila su
BAML**: las `Application.Resources` nunca se cargan y cualquier `StaticResource` de
una ventana falla en runtime (`XamlParseException`). Por eso los estilos compartidos
viven en los `Window.Resources` de cada ventana y `App.xaml` queda sin recursos.
Verificado empíricamente con plantilla `dotnet new wpf`: con `StartupUri` el BAML se
genera; sin él, no.

## Uso

- **Visor**: `dotnet run --project src/EvidenciasSQA.Wpf` — Captura / Copiar /
  Descargar / Editar → header horneado automáticamente. Rueda = zoom (anclado al
  cursor), arrastre = pan, doble carga de imagen = fit-to-window.
- **Toolbar del header** (tema claro Fluent, valores exactos inspeccionados de la
  app original):
  - Barra `#003060`, altura fija 64, padding horizontal 24, tipografía por defecto
    Segoe UI 16px blanca; franja inferior 3px `#FF6B00`; contenedor interno = Grid
    de 2 columnas (`*` | `Auto`) que mantiene ambos bloques pegados a sus bordes
    al redimensionar.
  - Bloque izquierdo (columna 0, centrado vertical): logo `sqa_` en caja naranja
    `#FF6B00` (59×32, `Margin="0,8,12,0"`), título contextual "Visor de
    Evidencias" (16 SemiBold) / "Historial de EvidenciasSQA", ayuda `?`, conmutador
    `Ver historial`/`Volver al visor` y navegación corta `Anterior | Siguiente`
    (deshabilitados en extremos, separador entre ambos).
  - Bloque derecho (columna 1, alineado a la derecha, separadores
    `Rectangle 1×16 #4A6B82` con `Margin="8,0"`):
    - Visor: `Editar | Copiar | Descargar | Eliminar | Recopilar` (Eliminar en
      `#DC3545`; Recopilar = captura de pantalla).
    - Historial normal: `Seleccionar | Generar Informe | Descargar Todo | Eliminar todo`.
    - Modos selección/informe: contador + acciones en lote / opciones de informe.
  - Todos los botones son enlaces planos (`HeaderLinkButtonStyle`): fondo
    transparente, texto blanco, cursor de mano; solo "Generar informe" y el
    conmutador destacan con `PrimaryHeaderButtonStyle`.
- **Editar**: botón "Editar" → abre el editor sobre un PNG temporal;
  Ctrl+S guarda in-place y el visor recarga en vivo (FileSystemWatcher debounced)
  y al cerrar el editor (evento `Exited`).
- **Editor standalone**: `EvidenciasSQA.Editor.Wpf.exe --file <ruta.png|.evidenciasSqa>`.
- **Verificación pixel-exacta**: test standalone en
  `%TEMP%\opencode\EvidenciasSQA-roundtrip` (gradiente, altura dinámica, quirk de
  detección y+4, roundtrip .evidenciasSqa con colores).

## Pendientes (prototipo → producción)

- Instalador/empaquetado conjunto (visitor + editor + logo) con electron-builder análogo.
- Integración con el flujo real de Evidencias SQA (archivo `Evidencia_NN.png`, SQLite).
- Enlace del acento `~(tilde)` vs paso de argumentos con espacios (ya manejado con
  comillas en `Arguments`).
- `SaveFileDialog` nativo pendiente de verificación en sesión normal (en sesiones
  remotas el diálogo Win32 común también cuelga; mitigado con `SQA_TEST_SAVEPATH`
  solo para pruebas).

## Lotes de Cambios (Changelog)

### Fase 18: Visor Zoom/Pan replicado (spec `Visor, Container, Zoom y Visualización de Imágenes.txt`)

El control `ZoomViewport` se integró en `MainWindow.xaml` (wiring completo con la
DP `Source`, el mensaje de estado de la info bar, botón "Restablecer zoom",
preservación de zoom+scroll en navegación ◀▶ y Escape). Replica la spec del
visor Electron: fit completo y centrado, zoom binario clic Fit↔100% contenido
dentro del contenedor (sin tapar el header), pan por arrastre (umbral 5 px),
Escape sale del zoom, porcentaje en la info bar ("Zoom: NN%").

- **Bug crítico 1 — zoom 1:1 roto por pHYs 300 DPI (corrección de Fase 15/16)**:
  el bake inyectaba pHYs 300 DPI (11811 px/m) replicando `injectPhysDpi` del
  worker del Electron. WPF con `Stretch=None` renderiza DIP = `px × 96/Dpi`,
  así que el zoom "1:1" mostraba la imagen al 32 % (614×362 en vez de 1920×1132)
  y `hsv` daba `false` (sin scrollbars). Fix: `PngPhysChunk.cs` pasa a
  `PixelsPerMeter = 3779` (96 DPI) con métodos renombrados `Inject96Dpi`
  (el reemplazo in-place corrige también PNGs viejos de 300 DPI);
  `CorporateHeader.cs` usa `SetResolution(96f, 96f)`; el Electron se corrigió
  en paralelo (`image-worker.js`: 11811 → 3779). El export Word usa dimensiones
  por píxel (no pHYs), sin impacto. Verificado: POST `/api/capture` →
  `Evidencias_08.png` con pHYs `xppm=3779 yppm=3779 unit=1` ✓.
- **Bug crítico 2 — pan muerto por eventos manejados del ScrollViewer**: los
  drags actuaban como clic (toggle de zoom) porque el `ScrollViewer` interno
  marca `MouseDown`/`MouseMove` como `Handled`, y `MouseLeftButtonDownEvent` es
  un evento DIRECTO (no burbujea al ancestro), por lo que el handler `+=` del
  `ZoomViewport` nunca se disparaba. Fix en el ctor del `ZoomViewport`:
  `AddHandler(Mouse.MouseDownEvent, (MouseButtonEventHandler)OnViewportMouseLeftButtonDown, true)`
  y `AddHandler(Mouse.MouseMoveEvent, (MouseEventHandler)OnViewportMouseMove, true)`
  (eventos genéricos burbujeantes con `handledEventsToo=true`); el
  `MouseLeftButtonUp` sigue con `+=`. Verificado: el drag ya no togglea y el pan
  se mueve con precisión.
- **Fix menor**: texto de ayuda del visor ("rueda = zoom") corregido a zoom
  binario por clic (el control no usa rueda; `ViewerViewModel.ShowHelp`).
- **Bordes redondeados de la captura (réplica del `img#screenshot` del Electron:
  `border-radius: 8px` + sombra `0 4px 20px rgba(0,0,0,.08)`)**: la `Image` del
  visor se envuelve en un `Border` (`CornerRadius=8`) con `RectangleGeometry`
  como clip que abraza EXACTAMENTE el rectángulo renderizado de la imagen
  (`UpdateImageSize`: Natural → tamaño natural en DIP `px×96/Dpi`; Fit → misma
  escala de `NotifyZoom`). El radio aplica a la imagen, no al viewport completo:
  en zoom 100 % (scroll) las esquinas de la captura siguen redondeadas. La sombra
  pasa del `Image` al `Border` (sigue la silueta redondeada). La sombra anterior
  (blur 15, depth 2, opacity 0.08) se mantiene como en Fase 18.
- **Verificación E2E (UIA, ventana 1300×800)**: ESC → fit "Zoom: 46%" con imagen
  completa y centrada (882×520, márgenes 179/178 H y 60/60 V — el 46 % es el
  render real height-limited; el "58 %" previo era ruido de medición) ✓ ·
  clic → zoom 100 % con imagen real 1920×1132 y scrollbars H/V ✓ · pan:
  drag −300 H → H=38 %, drag −220 V → V=34 %, drag +300 H → H=0 % (clamp en
  offset 0) ✓ · ESC → fit y scroll reseteado ✓ · el zoom nunca tapa el header
  (diff y0-63 = 0) ✓ · navegación ◀▶ preserva zoom+scroll ✓ · los 6 PNGs de
  `~/CapturasQA` reparchados a 96 DPI ✓.

### Fase 19: Cierre de gaps del Módulo de Selección (spec `Interfaz del Módulo de Selección (Historial).txt`)

Verificación E2E contra el checklist QA de la spec (10 pasos, UIA + ratón real +
teclado, ventana 1300×800, 9 capturas en `~/CapturasQA`). La base del módulo ya
existía (Fase 11/13); esta fase cierra los desvíos encontrados al contrastar el
código fuente Electron (`app.js:2178-2313` `updateHistoryUI`/`toggleSelectionMode`
y `UI.md` §5.5) y la spec:

- **Títulos sin sufijo "SQA"**: `Historial de Evidencias` / `Seleccionar
  Evidencias` / `Informe de Evidencias` (8 sitios en `ViewerViewModel`) — antes
  decían "…de EvidenciasSQA".
- **Botones lote**: `GenerateReportSelectedCommand` con `canExecute =
  SelectedIds.Count > 0` (antes siempre habilitado); `NotifyModeChanged()` ahora
  llama `CommandManager.InvalidateRequerySuggested()` (los `RelayCommand` usan
  `CommandManager.RequerySuggested`, que no se dispara con propiedades no-DP);
  toolbar ESTADO 2 reordenada a `[Cancelar] [Copiar] [Descargar] [Eliminar]` y
  ESTADO 4 a `[contador] [Cancelar] [Generar informe]` con labels cortos;
  estilos `SelectionActionButtonStyle` / `SelectionDangerActionButtonStyle` /
  `PrimarySelectionActionButtonStyle` (opacity 0.3 al deshabilitar, réplica del
  "opacity: .3" del CSS).
- **InfoBars al texto exacto de la spec** (3 mensajes: selección normal, opciones
  de informe, dual).
- **Bug crítico — rango Shift+Click colapsado**: `HandleTileClick` actualizaba
  `FocusedIndex` ANTES de `SelectRange`, por lo que `anchor = RangeAnchor ??
  FocusedIndex` siempre era el propio item clickeado (rango de 1). Fix: el foco
  se actualiza DESPUÉS de calcular el ancla (el ancla es el foco PREVIO, spec §4);
  el ancla se conserva entre Shift+Click sucesivos (`RangeAnchor = anchor`).
- **Bug — badges con gap al deseleccionar**: `ToggleSelect` en remove solo
  anulaba el badge del item; ahora llama `RefreshSelectionOrders()` (reindexa
  1..N, "gap cerrado" del checklist).
- **Crash de arranque con :3000 ocupado**: si la app Electron instalada del
  usuario corre y ocupa el puerto del listener HTTP, la app crasheaba con
  `HttpListenerException (32)`. Fix: `App.xaml.cs` envuelve la creación del
  `SqaHttpListener` en try/catch → log + `_sqaHttpListener = null` y la app
  continúa sin listener HTTP (degradación graceful; no se mata el proceso del
  usuario).
- **Nota E2E — contador SIEMPRE plural**: el checklist del .txt dice "1
  seleccionada" (singular), pero el código Electron (`updateHistoryUI`) usa
  `${size} seleccionadas` siempre; la réplica sigue al código (plural
  incondicional).
- **Nota E2E — medición de tiles**: los rects UIA de tiles NO visibles en el
  ScrollViewer virtualizado reportan coordenadas de contenido (fuera de la
  ventana); los clics de ratón deben apuntar a tiles con rect dentro del viewport
  (o usar `InvokePattern`, que funciona en cualquier scroll). El Shift físico se
  inyectó con `SendInput` VK_LSHIFT (definición canónica de INPUT, cb=40; la
  versión con union reducida fallaba con `ERROR_INVALID_PARAMETER` (87)).
- **Verificación E2E — 10/10 ✓**: entrada (título/infobar/contador 0/botones
  disabled) · orden 3,1,2 → contador 3 + badges ✓+1,2,3 + botones enabled ·
  deselección → contador 2, badges reordenados 1,2, badge 3 eliminado ·
  **Shift+Click rango 4..8 → "5 seleccionadas"** ✓ · teclado DOWN+ENTER → toggle
  (4) ✓ · ESC selección → normal ✓ · Informe → opciones (3 botones) ✓ · dual
  (infobar, "Generar informe" disabled→enabled) ✓ · ESC dual → opciones
  (permanece informe) ✓ · ESC opciones → normal ✓.

### Fase 21: Identidad SQA en tray/ventana + tipografía corporativa del header

Por petición del usuario (diseño de interfaces / tray + header):

- **Icono de bandeja y ventana desde `Media/SQA1.ico`**: `CreateTrayIcon()` en
  `MainForm.cs` prioriza `Media\SQA1.ico` (copiado al output por el csproj del
  tray, junto a `SQA1.png`); fallback a conversión PNG→Icon 32×32 y al icono del
  resx. La ventana principal del visor usa `Icon="Media/SQA1.ico"` (recurso
  `Media\SQA1.ico` en `EvidenciasSQA.Wpf.csproj`).
- **Título "Evidencias SQA"**: `application_title` en `language-es-ES.xml` /
  `language-en-US.xml` y `Title` de `MainWindow.xaml` (antes "Visor de
  EvidenciasSQA" / texto comercial largo).
- **Tipografía Semibold del header corporativo** (todos los bakers):
  - Título: familia **Segoe UI Semibold + Bold** (GDI+) / **ExtraBold 800**
    (WPF `FormattedText`) → ~15-20% más peso que Bold 700, sin cambiar tamaño
    (18px), márgenes ni alineación.
  - Origen: **Semibold (600)** 16px, misma familia que el título; el wrap mide
    con la misma fuente.
  - Meta: **Semibold (600)** 17px al 85% (antes Regular 400 → más cuerpo).
  - Helper `CreateUiFont`/`CreateFont` con candidatos
    `["Segoe UI Semibold", "Segoe UI"]` (nunca Light/Thin/Regular fina).
- **Validación empírica de emojis a color** (test de píxeles con el baker real):
  `CorporateHeaderBaker.Bake` produce emojis 📅🌐💻 a color
  (📅 rojo ≈ 5k px, 🌐 azul ≈ 1k px, 0 px monocromos) — la técnica GDI+ Segoe UI
  Emoji + AntiAlias es correcta en esta máquina incluso con `AntiAliasGridFit`/
  `ClearType`. Las capturas horneadas ANTES del fix conservan su header antiguo
  (el emoji queda incrustado en el PNG); solo las nuevas capturas muestran el
  color.

### Fase 20: Indicador de selección unificado (número en esquina superior izquierda)

Rediseño del badge de la tarjeta de galería por petición del usuario (diseño de
interfaces / WPF):

- **Eliminado** el badge de orden de la **esquina inferior derecha** (círculo
  22px `#003060` con `SelectionOrder`, antes visible en cualquier modo de
  selección).
- **Unificado** en la esquina superior izquierda: el círculo de selección (28px,
  anillo blanco + relleno translúcido `#4DFFFFFF` en modo selección) ahora
  muestra el **número de orden de selección** (`SelectionOrder`, 1..N) en vez del
  icono ✓, con fondo `#003060` vía `DataTrigger IsSelected=True`.
- **Binding/estado**: `Text="{Binding SelectionOrder}"` (int?; `null` fuera de la
  selección) + `Visibility="{Binding IsSelected, ... BoolToVis}"` + relleno
  `#003060` por trigger — el número aparece solo al estar seleccionado y se
  reordena con `RefreshSelectionOrders()` al deseleccionar/rango.
- **Verificación E2E (UIA, 1300×800)**: 0 selección → sin badges ✓ · 3
  seleccionadas → exactamente 3 badges numéricos {1,2,3} ✓ · todos en el corner
  top-left del tile (dx≈17, dy≈12; ninguno en la zona inferior) ✓ · deselección →
  reorden 1,2 ✓ · ESC → normal sin badges ✓.

### Fase 15: Réplica completa del header corporativo del Electron (WPF)

`CorporateHeaderBaker.cs` reescrito como réplica byte-a-byte del `drawHeader`
del `image-worker.js` del Electron (spec `Integración del Header Corporativo.txt`),
más inyección de pHYs 300 DPI (`PngPhysChunk.cs`).

- **Baker (`CorporateHeaderBaker.Bake(Image, CorporateHeaderMeta)`)**
  - Guard anti-OOM: devuelve `null` si la imagen supera 16384 px (replica
    `MAX_DIMENSION` del worker) → el header se omite sin romper el guardado.
  - Gradiente HORIZONTAL `#002b55 → #004080` (el Electron usa este azul; antes se
    usaba `#1E3A8A→#3B82F6`).
  - Franja naranja `#FF6B00` de 4 px SOLO en el borde inferior del header
    (antes se pintaban franjas superior e inferior).
  - Logo corporativo 65×45 centrado verticalmente (`src/Media/SQA1.png`, 245×133,
    copiado al output vía csproj con `<Link>Media\SQA1.png</Link>`); si no hay logo,
    placeholder naranja con "SQA" bold 18.
  - Título: 700/18 px blanco en (100,14) — default "Evidencia de prueba QA".
  - `"Origen: {url}"`: 600/16 px con `wrapTextAnywhere` (carácter a carácter,
    réplica exacta), línea 1 en y=41, interlineado 22, ancho `w-130`.
  - Meta: 400/17 px `rgba(255,255,255,0.85)` en y=`68+(lines-1)*22`:
    `"{ID} | 📅 {dd/mm/yyyy, hh:mm:ss tt} | 🌐 {browser} | 💻 {os}"`
    (defaults "ID: ---"/"Adjunto Local"/"N/A").
  - **Emojis a color (📅 U+1F4C5, 🌐 U+1F310, 💻 U+1F4BB)**: componente
    `ColorEmojiTextRenderer` (`EvidenciasSQA.Core/TextRendering/`): divide el texto
    en runs (texto/emoji) y dibuja cada emoji con la familia nativa "Segoe UI Emoji"
    (glifos COLR/CPAL) + `TextRenderingHint.AntiAlias` — GDI+ SÍ renderiza emoji a
    color (antes se omitían con la nota errónea "GDI+ no renderiza emojis de color").
  - **Baker WPF (`HeaderBakingService`, DrawingContext)**: WPF (FormattedText) NO
    soporta color emoji (monocromo/tofu) → la línea meta se renderiza con GDI+ a un
    `BitmapSource` congelado (`ColorEmojiTextRenderer.RenderToBitmapSource`) y se
    compone con `dc.DrawImage` — réplica visual del worker.
  - **Componente UI WPF (`EmojiTextBlock`, `EvidenciasSQA.Wpf/Controls/`)**: TextBlock
    con DP bindable `EmojiText` que incrusta cada emoji como imagen en línea (cache
    estático por codepoint+size). Estilos `MetadataBarTextStyle` /
    `MetadataBarTextOnDarkStyle` en `Styles.xaml` (Segoe UI Variable + emoji 1.15em
    + `TextOptions.TextFormattingMode=Display`). Uso:
    `<controls:EmojiTextBlock EmojiText="{Binding MetadataLine}" />` con la línea
    compuesta por `ColorEmojiTextRenderer.BuildMetaLine(...)`.
  - Altura dinámica `100+(lines-1)*22`; el contenido se dibuja en
    `destRect(0, dynHeaderHeight)` (bug corregido: se usaba `headerEnd`).
  - Detección de header previo (idempotencia): scan `x=min(20,w-1)`,
    `y=min(h-1,400)→0`, RGB EXACTO `(255,107,0)` → `headerEnd=y+4`; el header
    viejo se recorta y NO se re-hornea.
- **pHYs 300 DPI (`PngPhysChunk.cs`, nuevo)**: inyecta `x=y=11811 px/m, unit=1`
  tras IHDR (replica `injectPhysDpi` del worker, necesario para el render en Word).
  - **Fix GDI+**: el encoder PNG de GDI+ SIEMPRE escribe su propio pHYs (96 DPI =
    3779 px/m) al guardar con `ImageFormat.Png`. El estándar PNG dicta que el ÚLTIMO
    pHYs prevalece, así que insertar uno nuevo no basta: el chunk existente se
    REEMPLAZA in-place (x/y/unit + CRC32 recalculado) y solo se inserta si no hay.
  - Los fallos se registran con log4net (`[SQA-INTEGRATION]`), ya no son catch
    silenciosos.
- **`FileDestination.BakeCorporateHeader`**: PASO 6 = `PngPhysChunk.Inject300Dpi`
  tras el `File.Move` atómico (PASO 5). `App.xaml.cs` también inyecta pHYs en el
  flujo `HasHeader=true`.
- **Build**: usar SIEMPRE `dotnet build src\EvidenciasSQA\EvidenciasSQA.csproj`
  (directo) — el build de la SLN dejaba el binario STALE (no recompilaba el
  csproj: el PASO 6 quedó fuera del exe y la inyección nunca se aplicaba en
  runtime). Verificación post-build recomendada: los literales
  ("No se pudo inyectar pHYs 300 DPI") deben existir en el DLL (UTF-16).

**Verificación E2E (app en ejecución, POST /api/capture con PNG 640×360):**
`Evidencias_05.png` resultante 640×460 · gradiente `(0,43,86)` izq / `(0,63,126)`
der ✓ · franja naranja `(255,107,0)` en y=97-99 ✓ · contenido SteelBlue en y≥100 ✓
· chunk `pHYs` en offset 62 con `x=y=11811 (00 00 2E 23), unit=1, CRC válido` ✓ ·
PNG carga sin errores en System.Drawing ✓ · `dotnet build` 0 errores (55 warnings).

### Fase 16: Fix bake del logo cacheado (header omitido desde la 2ª captura)

Bug encontrado en verificación (18/08): la PRIMERA captura de cada instancia salía
horneada; TODAS las siguientes salían SIN header (tamaño crudo, sin pHYs) de forma
silenciosa (best-effort, `Log.Warn` con log4net sin configurar en runtime).

- **Causa raíz**: `CorporateHeaderBaker.Bake` envolvía `GetLogo()` en
  `using (Image logo = GetLogo())`. `GetLogo()` devuelve la instancia CACHEADA
  compartida (`_cachedLogo`, static); el `using` la DISPONÍA tras el primer bake.
  El segundo bake recibía una imagen inválida → `ArgumentException: Parameter is
  not valid` en `g.DrawImage(logo, ...)` (línea 136) → catch → header omitido.
  Stack real capturado vía log temporal:
  `Image.get_RawFormat()` ← `Graphics.DrawImage(...)` ← `Bake` ← `BakeCorporateHeader`.
- **Fix**: `GetLogo()` devuelve una COPIA fresca (`new Bitmap(_cachedLogo)`) en
  cada llamada (todas las ramas, incl. carga inicial). El `using` del llamador
  dispone la copia; el caché queda intacto. El logo vive por proceso (intencional).
- **Diagnóstico**: la excepción se capturó instrumentando `BakeCorporateHeader`
  con append-log temporal a `%TEMP%\sqa_bake_debug.log` (revertido tras el fix) —
  log4net no escribe en runtime (config embebida sin coincidir con el recurso).
- **Verificación E2E (build 11:44, app en ejecución)**: 2 POST consecutivos
  `/api/capture` → `Evidencias_14.png` y `Evidencias_15.png` AMBOS 640×460 horneados
  (~20.4 KB) con pHYs `x=y=11811 (00 00 2E 23)` ✓ (antes: 2º POST = crudo 640×360
  1803 B sin pHYs). Las capturas del tray usan el mismo `BakeCorporateHeader`.

### Fase 17: "Descargar Todo" con diálogo nativo "Guardar como..." (Downloads preseleccionada)

`ViewerViewModel.DownloadAll()` (botón "Descargar Todo" del historial) implementa
ahora la misma lógica que la descarga individual: dispara el `SaveFileDialog`
nativo del SO ("Guardar como...") para que el usuario elija la ubicación de
destino, con la carpeta `Downloads` preseleccionada por defecto (antes escribía
el zip directamente en `~/Downloads` sin preguntar).

- **Cambios en `DownloadAll()`**:
  - `SaveFileDialog` con `Title = "Descargar todas las evidencias"`,
    `FileName = Evidencias_DDMMAA.zip` (convención existente), `InitialDirectory`
    = `~/Downloads` (fallback a `UserProfile` si no existe), filtro ZIP.
  - Si el usuario cancela → `return` sin tocar nada (se eliminó la creación
    directa en Downloads y el auto-reveal con `explorer.exe /select`).
  - Aceptado → zip creado en `dialog.FileName` (misma lógica temp dir +
    `ZipFile.CreateFromDirectory`), sobrescribiendo si existe.
- **Verificación E2E (build 12:35, app en ejecución, vía UIA)**: invocar
  "Descargar Todo" → diálogo nativo class `#32770` título "Descargar todas las
  evidencias" ✓ · dirección "Dirección: Descargas" (aid 1001) + listado de
  Downloads ✓ · nombre pre-cargado `Evidencias_180826` ✓ · ESC cancela sin crear
  nada ✓ · Guardar → `~/Downloads/Evidencias_180826.zip` con las 3 evidencias
  del historial ✓ (Entradas=3, PNGs horneados con pHYs).
- **Nota**: "Descargar seleccionadas" (`DownloadSelectedCommand`) conserva su
  lógica previa; los diálogos individuales (`DownloadCurrent`, `DownloadTile`)
  ya usaban SaveFileDialog.

### Fase 14: Integración Ext_Web → Visor (listener HTTP loopback en :3000)

Integración completa de la extensión web `Ext_Web` con el visor WPF:
las capturas del navegador se persisten en `~/CapturasQA` y se muestran
en el visor por el MISMO bus que las capturas del Tray (`SqaEvents`).

- **`SqaHttpListener.cs` reescrito con el contrato real de la extensión**
  (`ext-web-visor-greenshot.md`):
  - Puerto por defecto **3000** (el que usa `Ext_Web`, hardcodeado en 6 archivos
    de la extensión), configurable vía env var `SQA_HTTP_LISTENER_PORT`.
  - Prefix `http://127.0.0.1:{port}/` (loopback-only, sin urlacl ni admin).
  - `POST /api/capture-binary` → body raw PNG + headers `X-SQA-Url/Title/Timestamp/
    Browser/OS/Has-Header` (Url se decodifica con `Uri.UnescapeDataString`).
  - `POST /api/capture` → JSON `{dataUrl,url,title,timestamp,browser,os,hasHeader}`
    (parseado con `System.Text.Json`, no con regex frágiles).
  - `POST /api/capture-batch` → JSON `{captures:[...]}` → un `CaptureReceived` por
    elemento + `results[{success}]` por índice (mismo tamaño que el array).
  - `GET /api/peek-sequence` → `{success,sequence,label}` SIN incrementar
    (nuevo `SqaEvidenceSequence.Peek(folder)` en Core: lock atómico, solo lectura).
  - `GET /api/show` → trigger UI-only `ViewerOpenRequested`.
  - `GET /api/health` → `{success}`.
  - Respuesta estándar de aceptación: `{success,status:"processing",autoCopyOnCapture:false}`
    — INMEDIATA (la persistencia corre en `Task.Run` del suscriptor).
- **Arranque y wiring en `App.xaml.cs`** (antes de que el visor cargue el historial):
  - `CaptureReceived` → `Task.Run(PersistIncomingCapture)`: escribe `Evidencias_NN.png`
    en `~/CapturasQA` (`SqaEvidenceSequence.Next`), hornea header si `HasHeader=false`,
    notifica con `SqaCaptureFlow().OnCaptureCompleted(path)` → `SqaEvents.CaptureSaved`
    → el visor refresca la galería (mismo flujo que el Tray, CaptureHelper).
  - `ViewerOpenRequested` → `SqaEvents.RaiseRestoreViewerRequested()` (el visor hace
    `ShowLastCapture()`).
- **Fix sistémico del bake de header en disco** (`FileDestination.BakeCorporateHeader`):
  GDI+ lanzaba `0x80004005` al sobrescribir el PNG con `baked.Save(fullPath)` porque el
  `Bitmap` original mantiene el lock del archivo (afectaba TAMBIÉN al flujo del Tray:
  las capturas se guardaban sin header). Ahora guarda en temporal y reemplaza con
  `File.Move(temp, fullPath, true)` DESPUÉS de liberar los handles (Paso 5).
- **Ext_Web: sin cambios** — el puerto 3000 y el contrato de respuesta ya eran los
  correctos; el listener ahora los cumple.

**Verificación E2E (app en ejecución, curl/Invoke-WebRequest):**
`GET /api/health` → 200 `{success}` · `GET /api/peek-sequence` → `{success,sequence:20,label:"Evidencias_20"}`
· `POST /api/capture` (dataUrl) → PNG persistido + header horneado verificado por
píxel (200×242, franja naranja `(255,107,0)` en y=2) · `POST /api/capture-binary`
con `X-SQA-Has-Header:false` → horneado; con `true` → idempotente (150×90 sin bake) ·
`POST /api/capture-batch` (3) → 3 PNG + `results` con 3 `success` · `GET /api/show` → 200.
`dotnet build` 0 errores (evidencias: `EvidenciasSQA.csproj`).

### Fase 13: Módulo seleccionar — badge de orden + copiar todas en orden

- **Badge de orden visible en selección:** el círculo `#003060` con el número
  de selección (1, 2, 3…) se mostraba solo en modo informe; ahora su visibilidad
  depende de `IsSelectionModeActive` (selección simple y dual informe+selección),
  replicando el `word-order-badge` de Electron que se actualiza al seleccionar
  (`MainWindow.xaml`).
- **Copiar seleccionadas copia TODAS en orden:** `CopySelected()` ya no usa
  `Clipboard.SetImage` en bucle (solo dejaba la última en el portapapeles). Ahora
  construye un `DataObject` con:
  - `DataFormats.Html` (CF_HTML estándar con offsets vía `BuildHtmlFragment`):
    un `<img>` base64 por evidencia, en el ORDEN de selección → Word/Chrome pegan
    todas las imágenes en ese orden.
  - `DataFormats.Bitmap`: primera evidencia (compatibilidad con apps de imagen
    única).
  - Formato propio `SQA_MULTI_IMAGE`: rutas en orden de selección.
- El orden proviene de `GetSelectedTiles()` (recorre `SelectedIds`, orden de
  inserción).

**Verificación:** `dotnet build` 0 errores (evidencias: `EvidenciasSQA.Wpf`,
`EvidenciasSQA.csproj`).

### Fase 12: Eliminación de la barra de zoom del visor

- `MainWindow.xaml`: eliminado el panel flotante de zoom (pill centrado abajo) con
  los botones `−`, `+`, `Ajustar` y `1:1` y el indicador "Zoom: 100%".
- `MainWindow.xaml.cs`: eliminados los handlers huérfanos
  `ZoomInButton_Click` / `ZoomOutButton_Click` / `FitButton_Click` /
  `ActualSizeButton_Click`.
- `MainWindow.xaml`: eliminado el estilo `ZoomButtonStyle` (sin uso).
- Se conservan el zoom con rueda del ratón (anclado al cursor), el pan con
  arrastre y el doble clic / Escape para ajustar a la ventana
  (`FastImageViewer`), así como el indicador `ZoomPercentText` de la barra de
  estado.

**Verificación:** `dotnet build` 0 errores (evidencias: `EvidenciasSQA.Wpf`,
`EvidenciasSQA.csproj`).

### Fase 11: Replica de especificacion-modulo-seleccion.md (selección completa)

Análisis del módulo de selección del visor WPF vs la especificación Electron y
replicación de los gaps:

- **Fix crítico (clic de selección roto):** el botón transparente de tarjeta y las
  acciones por tarjeta (Copiar/Descargar/Eliminar) bindeaban `IsEnabled` a
  `IsNormalMode`, que es `false` en modo selección/informe → el clic de selección
  NUNCA llegaba al `TileClickCommand`. Ahora bindean a `IsHistoryViewVisible`
  (`MainWindow.xaml`): el comando decide (toggle vs abrir visor), como el
  `div.onclick` de Electron.
- **Selección por rango (Shift+Click)** (replica app.js:3180-3205):
  - Nuevo estado `FocusedIndex` (int, -1 sin foco; setter sincroniza `IsFocused`
    en las tarjetas) y `RangeAnchor` (int?).
  - `HandleTileClick` consulta `Keyboard.Modifiers`: clic simple → toggle + ancla
    null; Shift+Click → `SelectRange(clicked, index)` desde `RangeAnchor ??
    FocusedIndex` hasta el item clickeado (ambos inclusive), sin duplicar ids ya
    seleccionados, expansión desde el MISMO ancla en Shift+Click sucesivos.
  - `RefreshSelectionOrders()` reindexa los badges de orden (1..N) tras rango y
    borrados (replica de `selectedOrderMap`).
- **Navegación por teclado de la galería** (replica app.js:3120-3190):
  - `MainWindow.OnPreviewKeyDown`: ↑/↓ mueven el foco (+`ScrollFocusedIntoView`
    vía `ItemContainerGenerator.ContainerFromIndex` + `BringIntoView`), Home/End
    van al primer/último item, Enter/Space → `SelectFocused()` (toggle si hay
    selección activa, abre el visor si no; el Space no roba el foco de los botones).
  - Escape ya existía (cancelar modo / reset zoom).
- **Foco visual:** nuevo `EvidenceTileModel.IsFocused` + DataTrigger en la tarjeta
  (borde `#FF6B00` 2px, replica de `.focused`).
- **Limpieza de estado:** `ClearSelection` y `ResetModes` resetean `FocusedIndex`
  y `RangeAnchor`; `DeleteSelected` apaga `IsSelectionModeActive` tras borrado
  parcial (paridad `deleteSelectedCaptures`); `DeleteTile` reindexa órdenes.

**Verificación:** `dotnet build` 0 errores (evidencias: `EvidenciasSQA.Wpf`,
`EvidenciasSQA.csproj`).

### Fase 10: Fix "Descargar Todo" — comando no ejecutaba el empaquetado ZIP

**Síntoma reportado post-QA:** el botón "Descargar Todo" (menú del historial) no hacía
nada al hacer clic; antes de migrar la descarga a ZIP sí funcionaba (descarga individual).

**Revisión de los 3 puntos críticos (WPF/MVVM):**

1. **CanExecute del comando:** `DownloadAllCommand = new RelayCommand(DownloadAll)` sin
   delegate `canExecute` → `RelayCommand.CanExecute` retorna `true` por defecto
   (`_canExecute?.Invoke(parameter) ?? true`). El canExecute NO bloqueaba.
2. **DataContext binding:** el botón usaba `Command="{Binding DownloadAllCommand}"`
   heredando el DataContext por el árbol visual. Sin `UserControl` intermedio el binding
   debería resolver, pero para eliminar cualquier ambigüedad (y por consistencia con los
   botones de tarjetas) TODOS los comandos de la toolbar del encabezado pasan a
   `Command="{Binding DataContext.XxxCommand, RelativeSource={RelativeSource AncestorType=Window}}"`.
3. **Excepción silenciosa:** `DownloadAll()` ya tenía try/catch externo con `StatusText`
   + trazas `[SQA-DOWNLOAD]` (Debug.WriteLine) en cada paso (ruta Descargas, conteo de
   tiles, copias, creación del ZIP, limpieza de temp). El catch reporta el error en
   `StatusText` en lugar de tragarlo.

**Cambios:**

- `MainWindow.xaml`: botones del StackPanel de historial normal (Seleccionar / Generar
  Informe / Descargar Todo / Eliminar todo) y del StackPanel del visor (Editar / Copiar /
  Descargar / Eliminar) bindeados con `RelativeSource={RelativeSource AncestorType=Window}`
  → resolución inequívoca al `ViewerViewModel`.
- `ViewerViewModel.cs` (fix previo mantenido): el setter de `IsHistoryViewVisible` ahora
  llama `NotifyModeChanged()` cuando cambia (garantiza que `IsNormalMode` —visibilidad del
  panel con "Descargar Todo"— se actualice al conmutar visor/historial).
- `DownloadAll()` (verificado): `Evidencias_DDMMAA.zip` en `%USERPROFILE%\Downloads`
  (fallback a `UserProfile` si no existe), sobrescritura si ya existe, temp con GUID,
  copia con `File.Copy(overwrite: true)` y ZIP con `ZipFile.CreateFromDirectory`,
  `StatusText` de éxito/error.

**Fix definitivo (el clic no llegaba al comando):** ante la persistencia del fallo sin
trazas (`Debug.WriteLine` invisible al ejecutar el exe sin depurador), se elimina la
dependencia del binding de comando:

- `MainWindow.xaml`: el botón "Descargar Todo" ya NO usa `Command="{Binding ...}"` — usa
  `Click="BtnDownloadAll_Click"` directo (code-behind), que invoca
  `vm.DownloadAll()` sin pasar por `CommandManager`/`RequerySuggested`.
- `MainWindow.xaml.cs`: nuevo handler `BtnDownloadAll_Click` (patrón idéntico a
  `HelpButton_Click` existente).
- `ViewerViewModel.cs`: `DownloadAll` pasa a `public` y todas las trazas
  `[SQA-DOWNLOAD]` se enrutan a `LogDownload()`, que escribe SIEMPRE en
  `%TEMP%\sqa_download.log` (visible sin depurador) además de `Debug`.
- **Feedback visual (cierre del diagnóstico):** tras crear el ZIP, se abre el
  Explorador de Windows con el archivo seleccionado
  (`explorer.exe /select,"ruta"`), de modo que el resultado es visible de forma
  inequívoca (antes se guardaba en silencio y parecía "no funcionar").

**Verificación:** `dotnet build` 0 errores (evidencias: `EvidenciasSQA.Wpf`,
`EvidenciasSQA.csproj`).

### Fase 9: Fix visor — imagen cortada/descentrada, zoom a esquina y empty state tras "Eliminar todo"

**Síntomas reportados post-QA:** (1) la imagen no se muestra completa ni centrada; (2) al
hacer clic en zoom la vista se desplaza a la esquina superior izquierda; (3) tras borrar
todas las capturas, el visor sigue mostrando la última captura aunque ya no exista.

**Causa raíz (1 y 2):** `OpenTileInViewer`/`LoadImageFromFile` asignan `CurrentImage`
mientras el visor está `Collapsed` (vista historial activa → `ActualWidth=0`). En
`FastImageViewer.OnSourceChanged`, `FitToWindow()` hacía early-return (`ComputeFitScale`
= 0) sin tocar `_inFitMode` ni `_scale`; si el usuario ya había hecho zoom/pan manual
(`_inFitMode=false`), el `SizeChanged` posterior llamaba `UpdatePosition()` en vez de
re-fitea → imagen a la escala anterior, cortada y con clamp a `[Pad + Area - img, Pad]`
(esquina sup-izq). **Causa raíz (3):** la rama `null` del parche anterior dejaba
`_inFitMode = false` (invertido) y la rama de imagen nueva no reseteaba las transformaciones.

**Cambios:**

- `FastImageViewer.cs`: nuevo `ResetTransforms()` (escala 1.0, translate 0, `_inFitMode =
  true` FORZADO). La rama de imagen nueva lo ejecuta ANTES de `FitToWindow()`: si el fit
  difiere por falta de tamaño, el `SizeChanged` posterior re-aplica el fit (determinismo
  total). La rama `null` (empty state) usa `ResetTransforms()` + `NotifyZoom()` (la barra
  de zoom vuelve a 100 %) en vez del `_inFitMode = false` invertido.
- `MainWindow.xaml`: overlay de empty state en el visor (fondo `#E8F0FE`, "Sin evidencias /
  Captura una pantalla para comenzar" — spec `especificacion-visor-estado-vacio.md` §1.1),
  visible SOLO con `CurrentImage == null` (`NullToVisible`).
- `Converters/NullToVisibleConverter.cs` (nuevo): `null → Visible`, valor → `Collapsed`
  (contraparte del `NullToCollapsedConverter` existente).

**Verificación:** `dotnet build` 0 errores (evidencias: `EvidenciasSQA.Wpf`).

### Fase 8: Borrado de TODAS las capturas (replica especificacion-borrar-todas-capturas.md)

Replica del flujo canónico de "Eliminar todo" (flujo A) y convergencia de los flujos
B/C al mismo estado final: **historial vacío + visor en empty state + navegación
incondicional al visor** (spec §1/§2.3).

- **`EvidenceRepository.ClearAll()`** (reemplaza `DeleteAll`): resultado transaccional
  `ClearAllResult(Success, DeletedCount, RemainingCount, Error)` con try/catch por
  archivo (fallo parcial no aborta el resto — spec §2.2). Conserva la guarda
  `maxFileNum === 0` (§5 #6): `RemainingCount > 0` impide que el consumidor vacíe
  estado o reinicie secuencias (borrado parcial → la galería re-escanea y reporta
  cuáles no se pudieron eliminar; nunca historial vacío con archivos huérfanos).
- **`SqaEvents.CapturesCleared`** (nuevo evento de dominio, §6.5): un único evento
  para visor/historial/seq, emitido SOLO con la carpeta realmente vacía.
- **`ViewerViewModel`**: `ClearAll` async (confirm → cursor Wait → borrado en
  `Task.Run` para no congelar el formulario §6.2 → limpieza transaccional vía
  `ApplyEmptyViewerState()`: `HistoryTiles.Clear`, `SelectedIds.Clear`,
  `ResetModes()` (selección/informe/módulo — obs. QA #2 cubierta), `_currentFilePath
  = null`, `_currentTileIndex = -1` (obs. QA #3), `CurrentImage = null` (empty state),
  `ShowViewer()`, `CommandManager.InvalidateRequerySuggested()`). Error → no muta
  nada (§5 #2). `DeleteCurrent`/`DeleteSelected` convergen al mismo estado si la
  galería quedó vacía (flujos B/C); en borrado parcial, si la captura activa del
  visor fue eliminada, el visor se vacía (consistencia §4).
- **Mapeo de capas sin equivalente en el visor-puro** (documentado): no hay contador
  `~/.last_id.txt` (la numeración vive en el productor de capturas, deshabilitado),
  ni SQLite/backend/Redis/IndexedDB — la fuente de verdad es el filesystem + el
  estado en memoria; la guarda del contador se traduce a `RemainingCount`.
- **Fix post-QA (visor mostraba la última captura tras "Eliminar todo")**:
  `FastImageViewer.OnSourceChanged` solo cambiaba el cursor al recibir `Source = null`;
  el bitmap anterior quedaba pintado aunque el archivo ya no existiera (historial
  vacío + visor con imagen NOK). Ahora el caso null replica `showEmptyState`:
  `CancelZoomAnimation` + `_image.Source = null` + reset de `_scale`/`_translate`/
  `_inFitMode` (estado vacío limpio, sin escala fantasma al cargar la siguiente).
- **Fix post-QA (botón "Ver historial" activo sin capturas)**: paridad con
  `updateUIState(false)` de Electron (deshabilita `historyLink`). Nueva propiedad
  `IsHistoryEnabled` en `ViewerViewModel` (galería en memoria + escaneo ligero de
  1 evidencia en disco) actualizada en arranque, `RefreshHistoryGrid`,
  `HandleCaptureSaved`, `ShowLastCapture`, borrados y `ApplyEmptyViewerState`
  (→ false explícito). El botón del header bindea `IsEnabled` con opacidad 0.3 al
  deshabilitarse (mismo estilo que los botones deshabilitados de Electron).
- **Fix post-QA (Cancel/Escape en módulo Informe)**: comportamiento contextual
  paridad con Electron:
  - **Opciones de informe** (sin selección): Cancel/Escape → **sale del modo
    informe** y vuelve al historial (`ResetModes()` + `IsHistoryViewVisible=true`).
  - **Selección dentro de informe**: Cancel/Escape → vuelve a **opciones** (mantiene
    `IsInformeModeActive`, limpia selección).
  - **Fase módulos**: Cancel/Escape → `CancelModuleExport()` → vuelve a opciones.
  - `MainWindow.OnPreviewKeyDown` (Escape) ya delega en `vm.CancelMode()` cuando
    `IsInformeModeActive`.

**Verificación:** `dotnet build` 0 errores.

### Fase 7: `ZoomViewport` — contrato binario Fit ↔ 100% (especificación Greenshot)

Implementación completa del contrato `viewer-container-zoom-greenshot.md` §2.2 +
`especificacion-visor-estado-vacio.md`, sobre el stub que existía para mantener el build:

- **`ZoomViewport.cs` (nuevo, WPF):** máquina de estados explícita `ViewerState { Empty,
  HasCapture }` + `ZoomMode { Fit, Natural }` con zoom BINARIO (clic = toggle, como el
  `.zoom-active` de Electron — sin rueda continua). API: `ShowImage(ImageSource)` /
  `ShowImage(string)` (FileStream con `FileShare.ReadWrite` + `BitmapCacheOption.OnLoad`
  + `Freeze` dentro de `using` → sin file-lock), `ToggleZoom`, `ResetZoom`,
  `PreserveZoomState`/`RestoreZoomState` (navegación ◀▶ con clamp automático de scroll),
  `ReleaseImage` (idempotente, libera Source → GC libera WIC), `IsBusy`, eventos
  `ZoomModeChanged`/`ImageReleased`.
- **Centrado por layout, no por matemática:** `ScrollViewer → Grid stretch → Image` con
  `Alignment=Center`; el Grid crece con el contenido en Natural (scrollbars Auto) y en
  Fit/Empty el layout centra solo. Cero coordenadas manuales → resize en Empty es O(1) y
  la "escala fantasma" (doc §4.1) es imposible por diseño.
- **Pan con umbral 5 px evaluado en `MouseMove`** (no en `MouseDown`, riesgo §6.5):
  `_wasDragging` distingue clic de arrastre; el clic tras arrastre NO alterna el zoom.
  Deltas incrementales (`ScrollToHorizontalOffset(offset − dx)`).
- **DPI (§6.2):** `Stretch=None` en WPF = 1 px imagen / 1 px físico a cualquier DPI
  (96/120/144); padding en DIPs (Fit 30 / Natural 40).
- **Escape:** el patrón ya existía en `MainWindow.OnPreviewKeyDown` (equivalente WPF del
  `KeyPreview` de WinForms): `e.Handled = true`; prioridad de modales automática (un
  `ShowDialog` captura su propio input y el `PreviewKeyDown` del principal no corre).
- **Coexistencia:** el host actual (`MainWindow.xaml`) sigue usando `FastImageViewer`
  (zoom continuo tipo Fotos). `ZoomViewport` queda compilado y disponible para adoptar
  el comportamiento binario de la especificación; decisión de intercambio pendiente.

**Verificación:** `dotnet build` 0 errores (evidencias: `EvidenciasSQA.Wpf`).

### Fase 6: Visor — imagen cortada/descentrada y zoom (fix doble escala)

**Síntoma:** la captura se veía ampliada (cortada) y no centrada; el zoom partía de un
estado incorrecto. Comportamiento deseado: como Fotos de Windows (fit-to-window).

**Causa raíz:** el `Image` interno del `FastImageViewer` se usaba con `Stretch="Uniform"`
y el control APLICABA ADEMÁS su autofit manual (`MaybeAutoFit` → `ScaleTransform`). Doble
escala (factor al cuadrado): con imágenes menores al área del visor quedaban ampliadas y
cortadas; con mayores, descentradas.

**Cambios:**

- `FastImageViewer.cs`: `Stretch` default vuelve a `Stretch.None` — el ajuste al área lo
  hace exclusivamente el `ScaleTransform` interno (escala = `min(wAncho/imgW, wAlto/imgH)`),
  centrando con `TranslateTransform`. Queda un solo factor de escala.
- `MainWindow.xaml`: eliminado `Stretch="Uniform"` del `FastImageViewer`; añadido
  `x:Name="ViewerControl"`.
- `FastImageViewer.cs` (pan): el bounding box del arrastre usa el tamaño NATURAL de la
  imagen (`Source.Width/Height * scale`) en vez de `_image.RenderSize` (que era el tamaño
  del control), para no permitir pan fuera de los bordes.
- Doble clic en el visor = ajustar a la ventana (comportamiento Fotos de Windows).
- `MainWindow.xaml.cs`: la tecla Escape en el visor ahora ejecuta `ViewerControl.ResetViewState()`
  de verdad (antes solo cambiaba el texto de estado).

**Barra de zoom tipo Fotos de Windows (ampliación posterior):**

- Botones flotantes centrados al pie del visor (pill semi-transparente): `−` (alejar),
  indicador `%` en vivo, `+` (acercar), `Ajustar` (fit-to-window) y `1:1` (tamaño real
  centrado). Zoom por botones anclado al CENTRO del visor (la rueda sigue anclada al cursor).
- `FastImageViewer.ZoomChanged` (evento con porcentaje) alimenta `ZoomPercentText` del VM,
  mostrado en la barra de estado junto al texto de estado, solo en vista visor.
- Cursor de MANO al pasar sobre la imagen con captura cargada (hover y arrastre); flecha
  sin imagen o al salir del área — como Fotos de Windows.
- Métodos públicos del control: `ZoomIn()`, `ZoomOut()`, `ResetViewState()` (fit),
  `ActualSize()` (1:1 centrado), evento `ZoomChanged`.

**Corrección de centrado y pan (1.0.3 final):** dos bugs residuales del control:

- **Clic desplazaba la imagen a la esquina superior izquierda:** el bounding box del drag
  forzaba `translate = 0` en cuanto el ratón se movía aunque la imagen estuviera centrada
  y no llenara el área. Ahora el pan es POR EJE: la dimensión que NO llena el área queda
  fija y centrada (`ApplyPan`/`CenterImage`); solo se puede arrastrar el eje que la excede.
- **Re-fit garantizado:** el fit ya no depende de un flag frágil (`_autoFitPending`); al
  cambiar `Source` se ejecuta `FitToWindow()` directo, y si el control aún no tenía tamaño
  (ventana oculta), el `SizeChanged` posterior lo aplica porque `_inFitMode` sigue true.
  El modo fit además se mantiene al REDIMENSIONAR la ventana (como Fotos); el zoom manual
  (rueda, botones, 1:1) sale del modo fit y el resize ya no altera la vista.
- Tras cualquier zoom (rueda/botones) que deje la imagen menor al área en algún eje, ese
  eje se re-centra automáticamente (sin "perderla" en una esquina).
- **Modelo de límites estricto (1.0.3 final):** el tamaño natural se obtiene con
  `PixelWidth * 96 / DpiX` (`BitmapSource`), no con `Width/Height` a secas — robusto ante
  capturas con DPI != 96 (144/192). Todos los límites (centrado, clamp de pan, fit) se
  calculan con `NaturalSize * Scale` vs `ActualWidth/ActualHeight`; nunca con `RenderSize`.
  El pan es por eje: la dimensión que no llena el área queda fija y centrada (la imagen no
  puede salirse del área); la que la excede se clampa a `[-(imgW - ActualW), 0]`.
- **SizeChanged re-ancla siempre:** si la ventana cambia de tamaño, en modo fit se re-fitea;
  con zoom manual (rueda/botones/1:1) se CONSERVA el zoom y solo se re-ancla la vista dentro
  del nuevo área (`KeepInBounds`) — la imagen nunca queda desplazada hacia esquinas
  invisibles tras un resize (el "74% con espacio en blanco / imagen fuera de pantalla").
- `ActualSize()` (1:1) centra de forma determinista: parte media visible si la imagen
  excede el área.

**Refinamiento UX (zoom suave, animación y padding — como Fotos de Windows):**

- **Smooth zoom anclado al cursor:** se anima SOLO la escala; el `TranslateTransform` se
  recalcula en CADA frame con `t = anchor + (s/s0)·(t0 - anchor)`, que mantiene el punto
  de la imagen bajo el cursor fijo durante todo el escalado (verificado por prueba
  headless: el píxel bajo el cursor es idéntico en todos los frames). Los límites
  (`UpdatePosition`) se aplican al terminar, sin interferir con la animación.
- **Motor de animación manual (DispatcherTimer ~15 ms, EaseOut cuadrático):** NO se usan
  `DoubleAnimation`/`BeginAnimation`. Los relojes internos de las animaciones WPF
  disparan eventos de `Freezable` (`Changed`/`Completed`) y, en caminos de
  cancelación/reemplazo (rueda rápida, fit, captura nueva), la desuscripción podía
  lanzar `ArgumentException: Handler has not been registered with this event` y matar
  el proceso. El motor manual hace `SetValue` directo de escala y translate en cada
  tick: el valor base del transform es SIEMPRE el valor actual (no hay valores
  "animados" fantasma que consolidar) y no existe ninguna suscripción a eventos de
  `Freezable` que limpiar. El `Tick` del timer se suscribe UNA vez en el constructor;
  la cancelación es solo `Stop()` + `_animating = false`. Re-entrante sin saltos.
- **Zoom animado en todos los caminos:** rueda (anclado al cursor), botones +/− (anclado
  al centro), "Ajustar"/doble clic/Escape (fit animado hacia el centro), 1:1 (centrado
  exacto). La carga de imagen nueva y el resize siguen siendo instantáneos (sin animación).
- **Padding interno (DP `Padding`, default 0; visor usa 12):** el área efectiva de
  cálculo es `ActualWidth/Height − Padding`. El fit se calcula sobre el área efectiva y
  el clamp de pan/zoom usa `[Pad + Área − img, Pad]`, por lo que la imagen nunca toca
  los bordes del control y conserva un margen libre, como Fotos de Windows.

**Comportamiento final del visor (equivalente a Fotos de Windows):** apertura con la imagen
completa y centrada (fit-to-window); rueda del ratón = zoom anclado al cursor; arrastre con
clic izquierdo = pan solo cuando la imagen excede el área, con cursor de mano; doble clic,
Escape o botón "Ajustar" = volver al ajuste; botones +/−/1:1 y % en la barra de estado.

### Fase 5: Clics del tray → menú de capturas + app solo en español

**Clics del icono del tray (MainForm):**

- Clic izquierdo y derecho sobre el icono: muestran SIEMPRE el menú contextual de
  capturas (clic derecho: menú nativo del NotifyIcon; clic izquierdo: se fuerza
  `ClickActions.SHOW_CONTEXT_MENU`).
- Eliminada la lógica de doble clic (`_doubleClickTimer`, `NotifyIconSingleClickTest`)
  y las llamadas a `SqaEvents.RaiseRestoreViewerRequested()` en los clics del icono.
- El visor SOLO se abre desde el menú → "Abrir Visor" (`Contextmenu_OpenVisorClick`),
  que sí dispara `ShowVisor()` + `RaiseRestoreViewerRequested()`.

**Solo español (idioma único):**

- `EvidenciasSQA.csproj`: el `None Include` de idiomas pasa de `Languages\language*.xml`
  (40 idiomas) a `Languages\language-es-ES.xml` — el output solo contiene el idioma español.
  (El `EvidenciasSQA.Editor.csproj` se revirtió al glob `language*.xml`: su carpeta
  `Languages\` está vacía y fijar un archivo inexistente rompía el build con error de copia.)
- `Language.cs` (Base): `DefaultLanguage` pasa de `en-US` a `es-ES` (fallback en
  instalaciones sin configuración previa; la config guardada sigue teniendo prioridad).
- `runtimes/` en el output: generada automáticamente por el SDK (runtime packs de las
  dependencias, p. ej. `sni.dll` de System.Data.SqlClient); solo se usa la rama `win-*`
  en Windows. No requiere limpieza manual; opcionalmente se puede podar fijando
  `RuntimeIdentifier=win-x64` en el proyecto.

### Fase 4: Header corporativo en el flujo directo del tray (bug: capturas sin cabecera)

**Síntoma:** una captura válida tomada desde el tray (región, pantalla completa, ventana,
última región) se guardaba **sin la cabecera corporativa**; el visor mostraba el PNG tal
cual.

**Causa raíz:** el flujo directo de guardado (`CaptureHelper.HandleCapture`) escribe el
PNG con `ImageIO.SaveRenderedImage` en un `Task.Run` (hilo de fondo) **sin hornear el
header** — el bake solo existía dentro de `FileDestination.ExportCapture`, que el flujo
directo nunca invoca. La notificación al visor, además, era un hack: los 5 handlers del
menú del tray esperaban 500 ms (`Task.Delay`) y elevaban `CaptureSaved` con el "PNG más
reciente de la carpeta" (`Directory.GetFiles` + `OrderByDescending(LastWriteTime)`), ruta
posiblemente incorrecta y siempre posterior al horneado inexistente.

**Solución — horneado + notificación en el punto de persistencia:**

- `FileDestination.cs`: `BakeCorporateHeader` pasa de `private` a `internal static` para
  reutilizarla desde el flujo directo (misma secuencia: escritura → horneado → validación
  → notificación).
- `CaptureHelper.cs` (flujo directo, dentro del `Task.Run` de guardado): tras
  `ImageIO.SaveRenderedImage` se ejecuta
  1. `FileDestination.BakeCorporateHeader(bgFullPath, captureDetails)` (best-effort,
     idempotente: si la captura ya trae la franja naranja no la repinta) y
  2. `new SqaCaptureFlow().OnCaptureCompleted(bgFullPath)` — punto único de notificación
     documentado del Core: valida existencia del archivo (con reintentos) y difunde
     `CaptureSaved` con la **ruta real del archivo guardado**, ya horneado.
- `MainForm.cs`: eliminados los 5 bloques `Task.Delay(500) + Directory.GetFiles +
  SqaEvents.RaiseCaptureSaved(files[0])` de los handlers de captura del menú del tray
  (área, pantalla completa, última región, ventana interactiva, ventana de la lista) — el
  evento ahora sale del flujo de guardado con la ruta correcta y tras el horneado.

**Resultado:** todo PNG persistido por el flujo directo queda con cabecera corporativa
(header + franja naranja + metadatos con el timestamp real de captura) y el visor se
notifica únicamente cuando el archivo ya está en disco y horneado.

**Corrección de deadlock (app congelada al capturar desde el tray):**

- **Síntoma:** tras la Fase 4, al capturar desde el tray la app se congelaba por completo.
- **Causa raíz:** el hilo de UI queda bloqueado en `Task.WaitAll(backgroundTasks)`
  (CaptureHelper.cs, `finally` del flujo directo) esperando el task de guardado en
  background; ese task ahora eleva `CaptureSaved` y `HandleCaptureSaved` (ViewerViewModel)
  hacía `dispatcher.Invoke` **síncrono** → el background esperaba a la UI → deadlock.
- **Fix:** `HandleCaptureSaved` usa `dispatcher.BeginInvoke` (asíncrono) cuando es invocado
  desde otro hilo: el productor nunca se bloquea esperando al Dispatcher, sea cual sea su
  origen (task de guardado en background, listener HTTP, tray).

**Quirk de build `EmojiDataTask` (MSB4092/instanciación de task):**

- **Síntoma:** "No se pudo crear una instancia de la tarea EmojiDataTask ... El tipo debe ser
  un tipo proporcionado en el runtime" al rebuildar la solución.
- **Causa raíz:** el target `GenerateEmojiTestTxtFile` recompilaba `EvidenciasSQA.BuildTasks`
  en cada build; si el nodo MSBuild persistente ya tenía la DLL cargada, TaskHostFactory no
  podía instanciar el tipo de la versión reescrita en disco.
- **Fix (EvidenciasSQA.csproj):** condición de staleness en el MSBuild inline — solo se
  recompila BuildTasks si la DLL falta o está desactualizada frente al csproj/fuente. Los
  timestamps se calculan como propiedades con `.Ticks` (las property functions
  `GetLastWriteTime` no son válidas dentro de condiciones) y se comparan con el operador
  relacional `<` (escape XML `&lt;`; MSBuild no admite `-lt` ni `LessThan`).
- **Workaround residual:** si el error reaparece (p. ej. tras editar fuentes de BuildTasks),
  `dotnet build-server shutdown` y rebuildar.

### Fase 3: Restauración del Visor con la última captura (bug: captura no visible con visor oculto)

**Síntoma:** con el visor oculto en segundo plano (bandeja), una captura tomada desde
el tray no se veía al restaurar el visor: la ventana se mostraba con la imagen
anterior/vacía porque no se recargaba la última evidencia.

**Solución — flujo de sincronización vía `SqaEvents.RestoreViewerRequested` + regla de oro:**

> **Regla de oro:** el visor NUNCA se trae al frente por una captura (el usuario captura
> sin interactuar con el visor). Cuando el usuario trae el visor al frente por CUALQUIER
> vía, se visualiza la última captura realizada/guardada en disco.

- `SqaEvents.cs`: nuevo evento `RestoreViewerRequested` + `RaiseRestoreViewerRequested()`
  (mismo patrón desacoplado que `CaptureSaved`: productor Tray → bus → consumidor Visor).
- `ViewerViewModel.cs`:
  - Suscripción al evento en el constructor (revierte en `Dispose`).
  - `HandleCaptureSaved()` (receptor de `CaptureSaved`): si el visor está **visible**,
    carga la captura en vivo **sin robar foco ni traer la ventana al frente**; si está
    **oculto/minimizado**, difiere la carga (`_currentFilePath = null` + StatusText) y
    NO muestra ni activa la ventana.
  - `ShowLastCapture()` (público, mariscal al hilo de UI): recupera la última evidencia
    del `EvidenceRepository` (`GetRecentEvidences(1)`), carga la imagen desde disco con
    `LoadImageFromFile` (si no hay evidencias, solo muestra la ventana con mensaje de
    estado) y asegura la visibilidad: `Show()` si está oculta al tray,
    `WindowState.Normal` si está minimizada, y `Activate()` para traerla al frente.
  - `HandleRestoreViewerRequested()`: delega en `ShowLastCapture()`.
- `MainForm.cs` (tray):
  - Menú contextual "Abrir Visor": además de `VisorWindowManager.ShowVisor()`, dispara
    `SqaEvents.RaiseRestoreViewerRequested()`.
  - Clic izquierdo (simple y doble) sobre el icono del tray: tras ejecutar la acción
    configurada (`LeftClickAction`/`DoubleClickAction`), dispara también
    `SqaEvents.RaiseRestoreViewerRequested()`.
- `App.xaml.cs`: hooks `IsVisibleChanged` (oculto → visible) y `StateChanged`
  (minimizado → normal) sobre la ventana del visor: al restaurar por CUALQUIER vía
  (barra de tareas, tray, menú) se ejecuta `ShowLastCapture()`.

**Salida real del tray ("Salir" no terminaba el proceso):**

- `MainForm.Exit()`: tras el cleanup WinForms (`Application.Exit()` no termina el
  proceso — el message pump es el Dispatcher WPF con `ShutdownMode.OnExplicitShutdown`),
  marca la salida real (`VisorWindowManager.SetQuitting()`) y apaga la Application WPF
  (`Application.Current.Shutdown()` vía Dispatcher).
- `VisorWindowManager`: nuevo estado `IsQuitting` (con `SetQuitting()`): durante la
  salida real la ventana del visor deja de cancelar su `Closing` (antes se ocultaba
  al tray y el `Application.Shutdown()` WPF quedaba cancelado → proceso vivo en el
  Administrador de tareas).
- `App.xaml.cs`: el handler `Closing` del visor solo cancela y oculta cuando NO es
  una salida real.

> **Nota de comportamiento:** con el clic configurado (ej. `SHOW_CONTEXT_MENU`), el visor
> se restaura tras cada clic izquierdo/doble clic del icono. Si resulta intrusivo, basta
> con mover el `RaiseRestoreViewerRequested()` al menú "Abrir Visor" únicamente.

### Fase 2: Atajos Globales + Bandeja del Sistema

**Hotkeys globales estilo EvidenciasSQA (implementados via `WndProc` + `RegisterHotKey`):**

| Atajo | Acción | Mapeo ViewModel |
|---|---|---|
| `PrintScreen` | Capturar región | `StartRegionCaptureAsync(false)` |
| `Alt+PrintScreen` | Capturar ventana activa | `CaptureActiveWindowCommand` |
| `Ctrl+PrintScreen` | Capturar todas las pantallas | `CaptureScreenCommand` |
| `Ctrl+Shift+V` | Pegar desde portapapeles | `PasteCommand` |

- `MainWindow.xaml.cs`: override de `OnSourceInitialized` registra 4 hotkeys globales
  vía P/Invoke (`RegisterHotKey`/`UnregisterHotKey`); hook de mensaje (`HwndSource.AddHook`)
  intercepta `WM_HOTKEY` y despacha por ID a los comandos del ViewModel.
- `ViewerViewModel.cs`: agregado comando público `CaptureActiveWindowCommand` y método
  privado `CaptureActiveWindow()` que ornea la captura con el header corporativo.
- `CaptureSource.cs`: agregado método `CaptureActiveWindow()` usando
  `GetForegroundWindow`/`GetWindowRect` (GDI+).

**Bandeja del Sistema (System Tray):**

- `App.xaml.cs`: el icono de la bandeja ahora muestra **icono** (`SQA1.png` como recurso),
  **tooltip** ("Evidencias SQA — Visor de Evidencias") y **menú contextual** con items:
  - `Mostrar` — restaura la ventana principal
  - `Inspeccionar Aplicación` — muestra ayuda/guía del visor
  - `Salir` — cierra la aplicación
- Doble clic en el icono también restaura la ventana.
- Cierre de la ventana (`Window_Closing`) intercepta el evento: si no es "Salir",
  oculta la ventana a la bandeja (comportamiento Electron `preventDefault` → `hide()`).
