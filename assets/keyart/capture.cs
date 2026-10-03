#:project ../../PinnyNotes.Core/PinnyNotes.Core.csproj
#:package System.Drawing.Common@10.0.0
#:property TargetFramework=net10.0-windows
#:property AllowUnsafeBlocks=true

// Seeds a portable Pinny Notes copy with demo notes, launches it and captures every window to PNG.
// usage: dotnet run capture.cs -- <appDir> <outDir> <light|dark>
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;
using PinnyNotes.Core;

string appDir = args[0], outDir = args[1], mode = args[2];
Directory.CreateDirectory(outDir);
string db = Path.Combine(appDir, "pinny_notes.sqlite");
foreach (var f in Directory.GetFiles(appDir, "pinny_notes.sqlite*")) File.Delete(f);
File.WriteAllText(Path.Combine(appDir, "portable.txt"), "");

string cs = $"Data Source={db};Pooling=False";
await DatabaseInitialiser.Initialise(cs);

long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
long min = 60_000, hour = 3_600_000, day = 86_400_000;
var notes = new (string content, double x, double y, double w, double h, string colour, bool pinned, bool open, long modified)[]
{
    ("Groceries\n• Oat milk\n• Sourdough\n• Basil + tomatoes\n• Coffee beans (dark roast)\n• Lemons ×3\n", 120, 120, 300, 270, "Yellow", true, true, now - 12 * min),
    ("Sprint 14 ✨\n\n1. Ship rounded note corners\n2. Notes list search\n3. Fluent delete dialog\n4. Write release notes\n", 460, 120, 320, 260, "Blue", false, true, now - 2 * hour),
    ("“Simplicity is prerequisite for reliability.”\n— Edsger W. Dijkstra\n", 820, 120, 300, 190, "Pink", false, true, now - 26 * hour),
    ("Wi-Fi guest\nSSID: Cafe-Upstairs\nPass: ask at the counter\n", 120, 440, 280, 180, "Green", false, true, now - 3 * day),
    ("Call Mum on Sunday 📞\nBook dentist appointment\nRenew library books\n", 460, 440, 300, 200, "Orange", false, true, now - 5 * day),
    ("Ideas\n- Dark mode wallpaper\n- Weekend hike: Ridge trail\n- Try the new ramen place\n", 820, 440, 290, 210, "Purple", false, false, now - 9 * day),
    ("{\n  \"name\": \"pinny\",\n  \"pinned\": true\n}\n", 1150, 440, 260, 170, "Aqua", false, false, now - 14 * day),
};

using (var c = new SqliteConnection(cs))
{
    c.Open();
    foreach (var n in notes)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO Notes (Content,X,Y,Width,Height,GravityX,GravityY,ThemeColourScheme,IsPinned,IsOpen,CreatedAt,ModifiedAt)
                            VALUES ($c,$x,$y,$w,$h,1,1,$s,$p,$o,$cr,$m)";
        cmd.Parameters.AddWithValue("$c", n.content);
        cmd.Parameters.AddWithValue("$x", n.x); cmd.Parameters.AddWithValue("$y", n.y);
        cmd.Parameters.AddWithValue("$w", n.w); cmd.Parameters.AddWithValue("$h", n.h);
        cmd.Parameters.AddWithValue("$s", n.colour);
        cmd.Parameters.AddWithValue("$p", n.pinned ? 1 : 0); cmd.Parameters.AddWithValue("$o", n.open ? 1 : 0);
        cmd.Parameters.AddWithValue("$cr", n.modified - day); cmd.Parameters.AddWithValue("$m", n.modified);
        cmd.ExecuteNonQuery();
    }
    var s = c.CreateCommand();
    s.CommandText = $@"UPDATE Settings SET Application_StartupBehaviour=0, Application_NewInstanceBehaviour=1,
        Application_CheckForUpdates=0, Notes_TransparencyMode=0, Notes_ColourMode={(mode == "dark" ? 1 : 0)}, Editor_SpellCheck=0";
    s.ExecuteNonQuery();
}

string exe = Path.Combine(appDir, "Pinny Notes.exe");
var proc = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = appDir })!;
await Task.Delay(4000);
// Second launch signals the running instance, which opens the notes list (NewInstanceBehaviour=ShowManagementWindow).
Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = appDir })!.WaitForExit();
await Task.Delay(3000);

int i = 0;
foreach (var (hwnd, title) in TopWindows(proc.Id))
{
    Native.GetWindowRect(hwnd, out var r);
    int w = r.Right - r.Left, h = r.Bottom - r.Top;
    if (w < 50 || h < 50) continue;
    using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        IntPtr hdc = g.GetHdc();
        Native.PrintWindow(hwnd, hdc, 2);
        g.ReleaseHdc(hdc);
    }
    string name = $"{mode}-{i++:D2}-{(string.IsNullOrWhiteSpace(title) ? "note" : title.Replace(' ', '_'))}-{r.Left}x{r.Top}.png";
    bmp.Save(Path.Combine(outDir, name), ImageFormat.Png);
    Console.WriteLine($"{name} {w}x{h}");
}

proc.Kill(true);

static List<(IntPtr, string)> TopWindows(int pid)
{
    var list = new List<(IntPtr, string)>();
    Native.EnumWindows((h, _) =>
    {
        Native.GetWindowThreadProcessId(h, out uint p);
        if (p == pid && Native.IsWindowVisible(h))
        {
            var sb = new StringBuilder(256);
            Native.GetWindowText(h, sb, 256);
            list.Add((h, sb.ToString()));
        }
        return true;
    }, IntPtr.Zero);
    return list;
}

static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
