# FocusGlow — Windows 11 Focus Mode

Десктопное приложение для Windows 11 на C# (.NET 8) со сверхплавными, аппаратно-ускоренными анимациями уровня macOS / Apple Intelligence (разблокированный фреймрейт 120 / 240+ Гц без ограничения в 60 FPS).

---

## Архитектура и стек рендеринга

1. **GPU-рендеринг через SkiaSharp (`SkiaSharp.Views.WPF`, `SKElement`)**:
   - Полный отказ от медленного CPU-шного `BlurEffect` WPF.
   - Отрисовка многопроходного свечения (Bloom / Neon Core) на GPU с фильтрами `SKMaskFilter.CreateBlur` и круговыми спектральными шейдерами `SKShader.CreateSweepGradient`.
   - Вырез активного окна через `canvas.ClipRoundRect(..., SKClipOperation.Difference)` с гладким сглаживанием углов.

2. **Анимационный цикл 240+ FPS**:
   - Привязан к `CompositionTarget.Rendering` с расчётом высокоточного `deltaTime` через `System.Diagnostics.Stopwatch.GetTimestamp()`.
   - Фреймрейт разблокирован глобально через `Timeline.DesiredFrameRateProperty.OverrideMetadata(...)` со значением `240` кадров/сек для поддержки дисплеев 120/144/240/360 Гц.

3. **Физика переходов**:
   - Асимметричные кривые Безье `cubic-bezier(0.22, 1, 0.36, 1)` (стандарт Apple Intelligence / iOS).
   - Пружинная физика (`DampedSpring`) и непрерывное сглаживание (`SmoothDamp`).
   - При прерывании анимаций (быстрое движение курсора туда и обратно) переход продолжается **с текущего промежуточного значения прозрачности без скачков**.

---

## Структура проекта

```
FocusGlow/
├── FocusGlow.csproj            # Конфигурация .NET 8, WPF, WinForms (для трея), SkiaSharp
├── app.manifest                # Манифест совместимости Windows 11 и Per-Monitor V2 DPI
├── App.xaml / App.xaml.cs      # Точка входа, Single-Instance Mutex, запуск 240Hz цикла
│
├── Interop/
│   └── Win32.cs                # P/Invoke: DwmGetWindowAttribute (EXTENDED_FRAME_BOUNDS),
│                               # GetCursorPos, WindowFromPoint, GetAncestor, LowLevel Hooks
├── Physics/
│   └── AnimationPhysics.cs     # Реализация CubicBezier (0.22, 1, 0.36, 1), DampedSpring, SmoothDamp
│
├── Models/
│   └── AppConfig.cs            # Модель настроек: горячие клавиши, боковые кнопки мыши, скругление
├── Services/
│   └── ConfigManager.cs        # Потокобезопасное чтение и сохранение в config.json
│
├── Rendering/
│   └── FocusOverlayRenderer.cs # SkiaSharp GPU-рендеринг: контурная комета, 4-проходный bloom,
│                               # динамический sweep gradient (#4285F4, #9B72CB, #D96570, #F4AF5F)
├── Core/
│   ├── FocusAnimationController.cs # Фазы анимации 1, 2 и 3, расчет прозрачности и шлейфа
│   └── FocusManager.cs         # Определение окна под курсором (без десктопа/таскбара), активация
│
├── Input/
│   └── GlobalInputHook.cs      # WH_KEYBOARD_LL + WH_MOUSE_LL (XButton1 / XButton2 / Esc)
│
├── UI/
│   └── TrayIconManager.cs      # NotifyIcon в трее, генерация иконки, контекстное меню
│
└── Views/
    ├── OverlayWindow.xaml/.cs  # Click-through, Layered, Topmost оверлей на весь VirtualScreen
    └── SettingsWindow.xaml/.cs # Окно настроек в тёмном стиле Win11 с записью горячих клавиш
```

---

## 3 Сценария анимации

- **Фаза 1 — Свечение по контуру (Apple Intelligence / Gemini style):**
  При активации вокруг окна проносится многоцветная комета (`#4285F4`, `#9B72CB`, `#D96570`, `#F4AF5F`) с мягким GPU-свечением (`SKMaskFilter.CreateBlur`), проходя периметр окна и растворяясь.
- **Фаза 2 — Двухэтапное затемнение фона:**
  На хвосте свечения фон за пределами активного окна плавно темнеет: сначала мягкий переход от `0%` до `50%` (~450 мс), затем медитативное дозатемнение от `50%` до `100%` (~1.5 с) до глубокого черного.
- **Фаза 3 — Реакция на уход и возврат курсора:**
  - Если курсор покидает целевое окно: затемнение плавно уходит в `0%` (~220 мс).
  - При возврате курсора: затемнение плавно возобновляется с **текущего промежуточного значения** (`текущее -> 50% -> 100%`).
  - Окно можно перемещать и изменять в размерах — оверлей отслеживает координаты в реальном времени через `DWMWA_EXTENDED_FRAME_BOUNDS`.
  - Если окно развернуто (`IsZoomed`), скругление автоматически становится `0px`, иначе `8px` (с учетом системного DPI).

---

## Запуск и сборка

Требуется: .NET 8 SDK (или новее).

```bash
# Сборка проекта
dotnet build -c Release

# Запуск
dotnet run -c Release
```
