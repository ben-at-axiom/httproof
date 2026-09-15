# Assets

## App icon

`app.svg` is source of truth. `app.ico` is generated multi-frame icon consumed by:

- `HTTProof.csproj` → `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` (Windows exe icon)
- `Views/MainWindow.axaml` → `Icon="/Assets/app.ico"` (title bar / taskbar)

Regen `app.ico` after editing `app.svg`:

```
magick -background none -density 384 HTTProof/Assets/app.svg -define icon:auto-resize=256,64,48,32,16 HTTProof/Assets/app.ico
```

Run from repo root. Requires ImageMagick 7+.

Frames produced: 16, 32, 48, 64, 256 px.

Windows exe icon **must** be `.ico` (PE format requirement). Avalonia `Window.Icon` accepts raster (PNG/BMP/JPG/ICO) but not SVG natively — reusing `app.ico` for both slots keeps things single-source.
