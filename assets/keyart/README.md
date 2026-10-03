# Key art & screenshots

`assets/keyart.png`, `assets/screenshot-light.png` and `assets/screenshot-dark.png` are built from real captures of the app.

1. Build a **Debug** copy (it uses a different single-instance mutex, so it won't talk to an installed Pinny Notes) and copy `PinnyNotes.WpfUi/bin/Debug/net10.0-windows` somewhere temporary.
2. Capture both themes. This seeds demo notes into a portable database, opens the notes and the notes list, and saves each window as a PNG:
   ```
   dotnet run capture.cs -- <copied app dir> <shots dir> light
   dotnet run capture.cs -- <copied app dir> <shots dir> dark
   ```
3. Copy the PNGs into `img/` next to the HTML files, named `<theme>-note-<x>-<y>.png` / `<theme>-Notes-<x>-<y>.png` (the capture file names minus the index), plus `icon.svg` from `assets/`.
4. Render with headless Edge or Chrome:
   ```
   msedge --headless=new --hide-scrollbars --force-device-scale-factor=1 --window-size=1280,640 --screenshot=keyart.png keyart.html
   msedge --headless=new --hide-scrollbars --force-device-scale-factor=1 --window-size=1280,720 --screenshot=screenshot-light.png "scene.html?mode=light"
   msedge --headless=new --hide-scrollbars --force-device-scale-factor=1 --window-size=1280,720 --screenshot=screenshot-dark.png "scene.html?mode=dark"
   ```

`keyart.png` is 1280×640, the size GitHub expects for a repository social preview.
