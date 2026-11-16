// ClaudeRing-Source 0.9.0  (자동 업데이트 검증용 표시 — 이 줄을 지우지 마세요)
// Claude 액션링 (Windows) - 1단계: 링 본체
// 빌드: build.bat 더블클릭 → ClaudeRing.exe 생성 (Windows 기본 .NET Framework 컴파일러 사용, 설치 필요 없음)
//
// - 단축키(기본 Ctrl+Alt+Shift+F12) → 마우스 커서 자리에 링이 터지듯 열림
//   (Logi Options+에서 휠 클릭에 이 단축키를 지정하면 '휠 클릭 → 링')
// - 9칸 중 5칸 기능, 4칸 빈칸. 가운데 코어 = Claude 열기
// - Esc / 바깥 클릭 / 단축키 다시 = 닫기. 우클릭 = 메뉴. 작업 표시줄 트레이 아이콘에서 종료
// - 설정: %USERPROFILE%\.claude_ring\config.json (링을 열 때마다 다시 읽음, 단축키 변경은 재시작 필요)
//
// C# 5 문법만 사용 (Windows 기본 csc.exe 호환)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using IO = System.IO;
using Shapes = System.Windows.Shapes;
using WinForms = System.Windows.Forms;

namespace ClaudeRing
{
    // ------------------------------------------------------------ 기능 정의
    class ActionDef
    {
        public string Id;
        public string Label;
        public string Icon;      // Segoe MDL2 Assets 글리프
        public string Pending;   // 아직 안 만든 기능일 때 보여 줄 안내
        public Action<RingApp> Handler;

        public ActionDef(string id, string label, string icon)
        {
            Id = id; Label = label; Icon = icon;
        }
    }

    // ------------------------------------------------------------ 설정
    class FolderSetting
    {
        public string Path = "";
        public bool Subfolders = true;   // 하위 폴더 색인
        public bool Watch = true;        // 변경 사항 감시
        public string Rescan = "daily";  // daily | interval | never
        public string Time = "03:00";    // 매일 재스캔 시각
        public int Hours = 6;            // 주기적 재스캔 간격(시간)

        public FolderSetting Clone() { return (FolderSetting)MemberwiseClone(); }
    }

    class Config
    {
        public string Hotkey = "ctrl+alt+shift+f12";
        public double Radius = 110;
        public string Accent = "#D97757";
        public double UiScale = 1.0;       // 글씨·창 크기 (0.8 ~ 1.5)
        public string Theme = "light";     // light | dark (TeamCal과 같은 색)
        public int DimPercent = 30;        // 링 열 때 배경 어둡게 (0 ~ 60)
        public string Animation = "normal"; // normal | fast | off
        public Dictionary<string, string> BubbleLabels = new Dictionary<string, string>(); // 버블 표시 이름
        public Dictionary<string, string> BubbleColors = new Dictionary<string, string>(); // 버블 색 (없으면 강조 색)
        public string SearchWindowPos = "cursor"; // cursor | center
        public bool SearchKeepQuery = true;
        public int SearchDefaultFilter = 0;
        public string OrganizeLastScheme = "";          // 지난번 정리 방법 (표시만, 매번 다시 고름)
        public bool OrganizeOldVersions = true;         // 옛 버전은 _old 로
        public bool OrganizeSubfolders = false;         // 하위 폴더 파일도 정리
        public string[] OrganizeRecent = new string[0]; // 최근 정리한 폴더
        public string CaptureFolder = "";               // 캡처 저장 폴더 (비우면 사진\ClaudeRing 캡처)
        public bool CaptureAutoSave = true;             // Win+Shift+S 후 자동 저장
        public string CaptureFormat = "png";            // png | jpg
        public string AskTarget = "app";                // app | web (물어보기를 어디서 열지)
        public bool AskIncludeClipboard = false;        // 물어보기에 클립보드 텍스트 함께 보내기 기본값
        public string MemoTarget = "sticky";            // sticky(스티커 메모) | notepad(메모장)
        public bool MemoNewNote = true;                 // 스티커 메모를 열 때 새 메모도 함께
        public string UpdateUrl = "";                   // version.json 주소 (비우면 업데이트 확인 안 함)
        public string WindowMonitor = "cursor";         // cursor | 1 | 2 ... (창을 띄울 모니터)
        public string WindowCorner = "cursor";          // cursor | center | topleft | topright | bottomleft | bottomright
        public string[] Slots = new string[] { "ask", "everything", "memo", "organize", "capture", null, null, null, null };
        public string ClaudeAppPath = "";
        public string EverythingPath = "";
        public string[] SearchRoots = new string[0];     // (고급) 비우면 모든 로컬 드라이브
        public bool IndexLocalDrives = true;             // 이 PC의 로컬 드라이브 모두 색인
        public bool IncludeMappedDrives = true;          // 네트워크 드라이브(Z: 등) 자동 포함
        public List<FolderSetting> Folders = DefaultFolders();
        public string[] SearchExclude = new string[] { "%WINDIR%", "%ProgramFiles%", "%ProgramFiles(x86)%", "%ProgramData%", "%USERPROFILE%\\AppData" };

        static List<FolderSetting> DefaultFolders()
        {
            return new List<FolderSetting>
            {
                new FolderSetting { Path = @"\\10.208.128.52\디자인2팀" },
                new FolderSetting { Path = @"\\10.208.128.52\설계1실" }
            };
        }

        public static string Dir
        {
            get { return IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude_ring"); }
        }

        public static string FilePath
        {
            get { return IO.Path.Combine(Dir, "config.json"); }
        }

        static Dictionary<string, string> StringMap(object v)
        {
            var d = new Dictionary<string, string>();
            var src = v as Dictionary<string, object>;
            if (src != null) foreach (var kv in src) if (kv.Value is string) d[kv.Key] = (string)kv.Value;
            return d;
        }

        static string Map(Dictionary<string, string> d)
        {
            return "{" + string.Join(", ", d.Select(kv => Q(kv.Key) + ": " + Q(kv.Value)).ToArray()) + "}";
        }

        static string[] Strings(object v)
        {
            var arr = v as object[];
            return arr == null ? new string[0] : arr.OfType<string>().ToArray();
        }

        public static Config Load()
        {
            var cfg = new Config();
            try
            {
                if (!IO.File.Exists(FilePath)) return cfg;
                var obj = new JavaScriptSerializer().DeserializeObject(IO.File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                if (obj == null) return cfg;
                object v;
                if (obj.TryGetValue("hotkey", out v) && v is string) cfg.Hotkey = (string)v;
                if (obj.TryGetValue("radius", out v) && v != null) cfg.Radius = Math.Max(80, Math.Min(220, Convert.ToDouble(v, CultureInfo.InvariantCulture)));
                if (obj.TryGetValue("accent", out v) && v is string) cfg.Accent = (string)v;
                if (obj.TryGetValue("ui_scale", out v) && v != null) cfg.UiScale = Math.Max(0.8, Math.Min(1.5, Convert.ToDouble(v, CultureInfo.InvariantCulture)));
                if (obj.TryGetValue("dim_percent", out v) && v != null) cfg.DimPercent = Math.Max(0, Math.Min(60, Convert.ToInt32(v, CultureInfo.InvariantCulture)));
                if (obj.TryGetValue("animation", out v) && v is string) cfg.Animation = (string)v;
                if (obj.TryGetValue("theme", out v) && v is string) cfg.Theme = (string)v;
                if (obj.TryGetValue("bubble_labels", out v)) cfg.BubbleLabels = StringMap(v);
                if (obj.TryGetValue("bubble_colors", out v)) cfg.BubbleColors = StringMap(v);
                if (obj.TryGetValue("search_window_pos", out v) && v is string) cfg.SearchWindowPos = (string)v;
                if (obj.TryGetValue("organize_last_scheme", out v) && v is string) cfg.OrganizeLastScheme = (string)v;
                if (obj.TryGetValue("organize_old_versions", out v) && v is bool) cfg.OrganizeOldVersions = (bool)v;
                if (obj.TryGetValue("organize_subfolders", out v) && v is bool) cfg.OrganizeSubfolders = (bool)v;
                if (obj.TryGetValue("organize_recent", out v)) cfg.OrganizeRecent = Strings(v);
                if (obj.TryGetValue("capture_folder", out v) && v is string) cfg.CaptureFolder = (string)v;
                if (obj.TryGetValue("capture_auto_save", out v) && v is bool) cfg.CaptureAutoSave = (bool)v;
                if (obj.TryGetValue("capture_format", out v) && v is string) cfg.CaptureFormat = (string)v;
                if (obj.TryGetValue("ask_target", out v) && v is string) cfg.AskTarget = (string)v;
                if (obj.TryGetValue("ask_include_clipboard", out v) && v is bool) cfg.AskIncludeClipboard = (bool)v;
                if (obj.TryGetValue("memo_target", out v) && v is string) cfg.MemoTarget = (string)v;
                if (obj.TryGetValue("memo_new_note", out v) && v is bool) cfg.MemoNewNote = (bool)v;
                if (obj.TryGetValue("update_url", out v) && v is string) cfg.UpdateUrl = (string)v;
                if (obj.TryGetValue("window_monitor", out v) && v is string) cfg.WindowMonitor = (string)v;
                if (obj.TryGetValue("window_corner", out v) && v is string) cfg.WindowCorner = (string)v;
                if (obj.TryGetValue("search_keep_query", out v) && v is bool) cfg.SearchKeepQuery = (bool)v;
                if (obj.TryGetValue("search_default_filter", out v) && v != null) { try { cfg.SearchDefaultFilter = Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch (Exception) { } }
                if (obj.TryGetValue("claude_app_path", out v) && v is string) cfg.ClaudeAppPath = (string)v;
                if (obj.TryGetValue("everything_path", out v) && v is string) cfg.EverythingPath = (string)v;
                if (obj.TryGetValue("search_roots", out v)) cfg.SearchRoots = Strings(v);
                if (obj.TryGetValue("search_exclude", out v) && v is object[]) cfg.SearchExclude = Strings(v);
                if (obj.TryGetValue("index_local_drives", out v) && v is bool) cfg.IndexLocalDrives = (bool)v;
                if (obj.TryGetValue("include_mapped_drives", out v) && v is bool) cfg.IncludeMappedDrives = (bool)v;

                if (obj.TryGetValue("search_folders", out v) && v is object[])
                {
                    var list = new List<FolderSetting>();
                    foreach (var o in (object[])v)
                    {
                        var d = o as Dictionary<string, object>;
                        if (d == null) continue;
                        var f = new FolderSetting();
                        object x;
                        if (d.TryGetValue("path", out x) && x is string) f.Path = (string)x;
                        if (d.TryGetValue("subfolders", out x) && x is bool) f.Subfolders = (bool)x;
                        if (d.TryGetValue("watch", out x) && x is bool) f.Watch = (bool)x;
                        if (d.TryGetValue("rescan", out x) && x is string) f.Rescan = (string)x;
                        if (d.TryGetValue("time", out x) && x is string) f.Time = (string)x;
                        if (d.TryGetValue("hours", out x) && x != null) { try { f.Hours = Math.Max(1, Math.Min(168, Convert.ToInt32(x, CultureInfo.InvariantCulture))); } catch (Exception) { } }
                        if (f.Path.Trim().Length > 0) list.Add(f);
                    }
                    cfg.Folders = list;
                }
                else if (obj.TryGetValue("network_roots", out v) && Strings(v).Length > 0)
                {
                    // 예전 설정(network_roots)에서 옮겨 오기
                    cfg.Folders = Strings(v).Select(p => new FolderSetting { Path = p }).ToList();
                }

                if (obj.TryGetValue("slots", out v) && v is object[])
                {
                    var arr = (object[])v;
                    var slots = new string[9];
                    for (int i = 0; i < 9; i++) slots[i] = i < arr.Length ? arr[i] as string : null;
                    cfg.Slots = slots;
                }
            }
            catch (Exception)
            {
                // 설정 파일이 깨졌으면 기본값으로
            }
            return cfg;
        }

        static string Q(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        static string B(bool b) { return b ? "true" : "false"; }

        public void Save()
        {
            IO.Directory.CreateDirectory(Dir);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"hotkey\": " + Q(Hotkey) + ",");
            sb.AppendLine("  \"radius\": " + Radius.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"accent\": " + Q(Accent) + ",");
            sb.AppendLine("  \"ui_scale\": " + UiScale.ToString("0.##", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"dim_percent\": " + DimPercent.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"animation\": " + Q(Animation) + ",");
            sb.AppendLine("  \"theme\": " + Q(Theme) + ",");
            sb.AppendLine("  \"bubble_labels\": " + Map(BubbleLabels) + ",");
            sb.AppendLine("  \"bubble_colors\": " + Map(BubbleColors) + ",");
            sb.AppendLine("  \"search_window_pos\": " + Q(SearchWindowPos) + ",");
            sb.AppendLine("  \"organize_last_scheme\": " + Q(OrganizeLastScheme) + ",");
            sb.AppendLine("  \"organize_old_versions\": " + B(OrganizeOldVersions) + ",");
            sb.AppendLine("  \"organize_subfolders\": " + B(OrganizeSubfolders) + ",");
            sb.AppendLine("  \"organize_recent\": [" + string.Join(", ", OrganizeRecent.Select(x => Q(x)).ToArray()) + "],");
            sb.AppendLine("  \"capture_folder\": " + Q(CaptureFolder) + ",");
            sb.AppendLine("  \"capture_auto_save\": " + B(CaptureAutoSave) + ",");
            sb.AppendLine("  \"capture_format\": " + Q(CaptureFormat) + ",");
            sb.AppendLine("  \"ask_target\": " + Q(AskTarget) + ",");
            sb.AppendLine("  \"ask_include_clipboard\": " + B(AskIncludeClipboard) + ",");
            sb.AppendLine("  \"memo_target\": " + Q(MemoTarget) + ",");
            sb.AppendLine("  \"memo_new_note\": " + B(MemoNewNote) + ",");
            sb.AppendLine("  \"update_url\": " + Q(UpdateUrl) + ",");
            sb.AppendLine("  \"window_monitor\": " + Q(WindowMonitor) + ",");
            sb.AppendLine("  \"window_corner\": " + Q(WindowCorner) + ",");
            sb.AppendLine("  \"search_keep_query\": " + B(SearchKeepQuery) + ",");
            sb.AppendLine("  \"search_default_filter\": " + SearchDefaultFilter.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"slots\": [" + string.Join(", ", Slots.Select(s => Q(s)).ToArray()) + "],");
            sb.AppendLine("  \"claude_app_path\": " + Q(ClaudeAppPath) + ",");
            sb.AppendLine("  \"everything_path\": " + Q(EverythingPath) + ",");
            sb.AppendLine("  \"index_local_drives\": " + B(IndexLocalDrives) + ",");
            sb.AppendLine("  \"include_mapped_drives\": " + B(IncludeMappedDrives) + ",");
            sb.AppendLine("  \"search_roots\": [" + string.Join(", ", SearchRoots.Select(x => Q(x)).ToArray()) + "],");
            sb.AppendLine("  \"search_folders\": [");
            for (int i = 0; i < Folders.Count; i++)
            {
                var f = Folders[i];
                sb.Append("    { \"path\": " + Q(f.Path) + ", \"subfolders\": " + B(f.Subfolders) + ", \"watch\": " + B(f.Watch)
                    + ", \"rescan\": " + Q(f.Rescan) + ", \"time\": " + Q(f.Time) + ", \"hours\": " + f.Hours.ToString(CultureInfo.InvariantCulture) + " }");
                sb.AppendLine(i < Folders.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"search_exclude\": [" + string.Join(", ", SearchExclude.Select(x => Q(x)).ToArray()) + "]");
            sb.AppendLine("}");
            IO.File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }
    }

    // ------------------------------------------------------------ Windows API
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int index);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder t, int max);

        public static void SendCtrlN()
        {
            const byte CTRL = 0x11, N = 0x4E; const uint UP = 0x2;
            keybd_event(CTRL, 0, 0, IntPtr.Zero);
            keybd_event(N, 0, 0, IntPtr.Zero);
            keybd_event(N, 0, UP, IntPtr.Zero);
            keybd_event(CTRL, 0, UP, IntPtr.Zero);
        }

        // Win+Shift+S (스크린 스니핑) 보내기
        public static void SendWinShiftS()
        {
            const byte WIN = 0x5B, SHIFT = 0x10, S = 0x53;
            const uint UP = 0x2;
            keybd_event(WIN, 0, 0, IntPtr.Zero);
            keybd_event(SHIFT, 0, 0, IntPtr.Zero);
            keybd_event(S, 0, 0, IntPtr.Zero);
            keybd_event(S, 0, UP, IntPtr.Zero);
            keybd_event(SHIFT, 0, UP, IntPtr.Zero);
            keybd_event(WIN, 0, UP, IntPtr.Zero);
        }

        // 마우스 아래에 있는 최상위 창
        public static IntPtr WindowUnderCursor()
        {
            POINT p;
            GetCursorPos(out p);
            IntPtr h = WindowFromPoint(p);
            return h == IntPtr.Zero ? h : GetAncestor(h, 2); // GA_ROOT
        }

        public static string ClassOf(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHARE_INFO_1
        {
            public string shi1_netname;
            public uint shi1_type;
            public string shi1_remark;
        }

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetShareEnum(string serverName, int level, out IntPtr bufPtr, int prefMaxLen, out int entriesRead, out int totalEntries, ref int resumeHandle);

        [DllImport("Netapi32.dll")]
        static extern int NetApiBufferFree(IntPtr buf);

        // "\\서버" 처럼 공유 폴더 이름 없이 서버만 적었는지
        public static bool IsServerOnly(string path)
        {
            if (path == null || !path.StartsWith(@"\\")) return false;
            string rest = path.Substring(2).TrimEnd('\\');
            return rest.Length > 0 && rest.IndexOf('\\') < 0;
        }

        // 서버의 공유 폴더 목록 (관리용 $ 공유 제외). 실패하면 오류 코드를 돌려줌
        public static List<string> ListShares(string server, out int error)
        {
            var list = new List<string>();
            string name = server.TrimEnd('\\');
            IntPtr buf;
            int read, total, resume = 0;
            error = NetShareEnum(name, 1, out buf, -1, out read, out total, ref resume);
            if (error == 0 || error == 234)
            {
                try
                {
                    int size = Marshal.SizeOf(typeof(SHARE_INFO_1));
                    for (int i = 0; i < read; i++)
                    {
                        var info = (SHARE_INFO_1)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + i * size), typeof(SHARE_INFO_1));
                        bool disk = (info.shi1_type & 0xFF) == 0;
                        bool special = (info.shi1_type & 0x80000000) != 0;
                        if (disk && !special && !info.shi1_netname.EndsWith("$"))
                            list.Add(name + "\\" + info.shi1_netname);
                    }
                }
                finally { NetApiBufferFree(buf); }
                error = 0;
            }
            return list;
        }

        public static string ShareError(int code)
        {
            if (code == 5) return "접근 권한이 없어요 (로그인이 필요할 수 있어요)";
            if (code == 53 || code == 67) return "서버를 찾을 수 없어요 (네트워크·VPN 확인)";
            if (code == 1326 || code == 1331 || code == 1907) return "서버에 로그인이 필요해요";
            if (code == 2114) return "서버에서 공유 서비스가 꺼져 있어요";
            return "연결 오류 " + code;
        }

        public static double DpiScale()
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            int dpi = GetDeviceCaps(dc, 88); // LOGPIXELSX
            ReleaseDC(IntPtr.Zero, dc);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }

        public static bool ParseHotkey(string text, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var raw in text.ToLowerInvariant().Replace(" ", "").Split('+'))
            {
                string p = raw;
                if (p == "alt") mods |= 0x1;
                else if (p == "ctrl" || p == "control") mods |= 0x2;
                else if (p == "shift") mods |= 0x4;
                else if (p == "win") mods |= 0x8;
                else if (p.Length >= 2 && p[0] == 'f' && p.Substring(1).All(char.IsDigit))
                {
                    int n = int.Parse(p.Substring(1));
                    if (n >= 1 && n <= 24) vk = (uint)(0x70 + n - 1);
                }
                else if (p == "space") vk = 0x20;
                else if (p == "insert") vk = 0x2D;
                else if (p == "home") vk = 0x24;
                else if (p == "end") vk = 0x23;
                else if (p == "pause") vk = 0x13;
                else if (p.Length == 1 && char.IsLetterOrDigit(p[0])) vk = (uint)char.ToUpperInvariant(p[0]);
            }
            return vk != 0;
        }
    }

    // ------------------------------------------------------------ 앱
    class RingApp
    {
        public Application App;
        public Dispatcher Dispatcher { get { return App.Dispatcher; } } // UI 스레드로 보내는 용도 (Updater 등)
        public Config Cfg;
        public Dictionary<string, ActionDef> Actions = new Dictionary<string, ActionDef>();
        public Point LastPoint; // 링을 마지막으로 연 위치 (기능 창을 그 근처에 띄울 때 사용)
        RingWindow ring;
        WinForms.NotifyIcon tray;
        IntPtr hwnd;

        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            var mutex = new Mutex(true, "ClaudeActionRing_SingleInstance", out created);
            if (!created)
            {
                MessageBox.Show("액션링이 이미 실행 중이에요.\n작업 표시줄 오른쪽 트레이 아이콘에서 확인할 수 있어요.", "Claude 액션링");
                return;
            }
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RingApp ra = null;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log("치명적 오류: " + e.ExceptionObject);
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log("화면 오류: " + e.Exception);
                e.Handled = true; // 프로그램이 꺼지지 않게
                if (ra != null) ra.Toast("오류가 났어요. ClaudeRing_log.txt 에 기록했어요", 3500);
            };
            Log("시작 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ra = new RingApp(app);
            ra.Start(args.Contains("--demo"));
            app.Run();
            GC.KeepAlive(mutex);
        }

        public const string APP_VERSION = "0.9.0";
        public const string DEFAULT_UPDATE_URL = "https://none-hongcheol.github.io/Claudering/version.json";

        public volatile bool UpdateAvailable;
        public string UpdateVersion, UpdateNotes, UpdateFileUrl;

        public string EffectiveUpdateUrl
        {
            get { return string.IsNullOrEmpty((Cfg.UpdateUrl ?? "").Trim()) ? DEFAULT_UPDATE_URL : Cfg.UpdateUrl.Trim(); }
        }

        public void PromptUpdate()
        {
            if (!UpdateAvailable) { OpenGeneralSettings(); return; }
            Updater.PromptAndApply(this);
        }

        public static void Log(string text)
        {
            try
            {
                string path = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClaudeRing_log.txt");
                if (IO.File.Exists(path) && new IO.FileInfo(path).Length > 512 * 1024) IO.File.Delete(path);
                IO.File.AppendAllText(path, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception) { }
        }

        public RingApp(Application app)
        {
            App = app;
            Add(new ActionDef("ask", "물어보기", "") { Pending = "Claude 물어보기는 따로 연결할 예정이에요" });
            Add(new ActionDef("everything", "파일 검색", ""));
            Add(new ActionDef("memo", "메모", ""));
            Add(new ActionDef("organize", "파일 정리", ""));
            Add(new ActionDef("capture", "화면 캡처", ""));
            // 기능을 만들면 여기서 Handler 를 연결합니다.
            Actions["everything"].Handler = FileSearchWindow.Open;
            Actions["organize"].Handler = OrganizeWindow.Open;
            Actions["capture"].Handler = Capture.Start;
            Actions["ask"].Handler = AskWindow.Open;
            Actions["memo"].Handler = Memo.Open;
        }

        void Add(ActionDef a) { Actions[a.Id] = a; }

        void Start(bool demo)
        {
            Cfg = Config.Load();
            if (!IO.File.Exists(Config.FilePath))
            {
                try { Cfg.Save(); } catch (Exception) { }
            }

            Theme.Load(Cfg);
            FileIndex.Start(Cfg);

            ring = new RingWindow(this);
            hwnd = new WindowInteropHelper(ring).EnsureHandle();
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);

            tray = new WinForms.NotifyIcon();
            tray.Icon = System.Drawing.SystemIcons.Application;
            bool ok = ApplyHotkey(Cfg.Hotkey);
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("링 열기", null, (s, e) => ring.ShowAtScreenCenter());
            menu.Items.Add("설정", null, (s, e) => OpenSettings(null));
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("종료", null, (s, e) => Quit());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ring.ShowAtScreenCenter();
            tray.Visible = true;

            Log("단축키 " + Cfg.Hotkey + (ok ? " 등록됨" : " 등록 실패"));
            if (!ok)
                Toast("단축키(" + Cfg.Hotkey + ")를 등록하지 못했어요. 다른 프로그램이 쓰고 있을 수 있어요.", 5000);
            else
                Toast("액션링 준비 완료 · " + Cfg.Hotkey.ToUpperInvariant(), 2400);

            // 업데이트 확인: 켤 때 한 번, 이후 30분마다
            if (!string.IsNullOrEmpty(EffectiveUpdateUrl))
            {
                var ut = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
                ut.Tick += (s, e) => Updater.Check(this, false);
                ut.Start();
                var once = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                once.Tick += (s, e) => { once.Stop(); Updater.Check(this, false); };
                once.Start();
            }

            if (demo) ring.ShowAtScreenCenter();
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312) // WM_HOTKEY
            {
                if (!ring.IsVisible)
                {
                    LastForeground = Native.GetForegroundWindow();
                    LastUnderMouse = Native.WindowUnderCursor();
                }
                if (ring.IsVisible) ring.HideRing(); else ring.ShowAtCursor();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void RunAction(string id)
        {
            ActionDef a;
            if (!Actions.TryGetValue(id, out a)) return;
            if (a.Handler != null)
            {
                try { a.Handler(this); }
                catch (Exception ex) { Log(a.Label + " 오류: " + ex); Toast(a.Label + " 실행 중 오류: " + ex.Message, 5000); }
            }
            else
            {
                Toast(a.Pending ?? ("'" + a.Label + "'은(는) 다음 단계에서 만들 기능이에요"), 2400);
            }
        }

        public void OpenClaude()
        {
            string path = Cfg.ClaudeAppPath;
            if (string.IsNullOrEmpty(path) || !IO.File.Exists(path))
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] candidates = {
                    IO.Path.Combine(local, "AnthropicClaude", "claude.exe"),
                    IO.Path.Combine(local, "Programs", "Claude", "Claude.exe")
                };
                path = candidates.FirstOrDefault(IO.File.Exists);
            }
            try
            {
                if (!string.IsNullOrEmpty(path)) Process.Start(path);
                else Process.Start("https://claude.ai");
            }
            catch (Exception ex)
            {
                Toast("Claude를 열지 못했어요: " + ex.Message, 4000);
            }
        }

        public IntPtr LastForeground; // 링을 열기 직전에 앞에 있던 창 (탐색기면 그 폴더를 씀)

        static object Prop(object o, string name)
        {
            return o == null ? null : o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null);
        }

        public IntPtr LastUnderMouse; // 링을 열 때 마우스 아래에 있던 창

        // 링을 열 때 마우스 아래 창 → 없으면 맨 앞 창 순서로 탐색기 폴더를 찾음
        public string ExplorerFolder()
        {
            return ExplorerFolderFor(LastUnderMouse) ?? ExplorerFolderFor(LastForeground);
        }

        // 창 하나가 탐색기(또는 바탕화면)이면 그 폴더 경로, 아니면 null
        public string ExplorerFolderFor(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return null;
                string cls = Native.ClassOf(hwnd);
                if (cls == "Progman" || cls == "WorkerW") return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                long target = hwnd.ToInt64();
                var t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) return null;
                object shell = Activator.CreateInstance(t);
                object wins = t.InvokeMember("Windows", BindingFlags.InvokeMethod, null, shell, null);
                int count = Convert.ToInt32(Prop(wins, "Count"));
                for (int i = 0; i < count; i++)
                {
                    object w = wins.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, wins, new object[] { i });
                    if (w == null) continue;
                    long h;
                    try { h = Convert.ToInt64(Prop(w, "HWND")); } catch (Exception) { continue; }
                    if (h != target) continue;
                    object self = Prop(Prop(Prop(w, "Document"), "Folder"), "Self");
                    string path = Prop(self, "Path") as string;
                    if (!string.IsNullOrEmpty(path) && IO.Directory.Exists(path)) return path;
                }
            }
            catch (Exception ex) { Log("탐색기 폴더 확인 실패: " + ex.Message); }
            return null;
        }

        public string LabelFor(string id) { return LabelFor(id, Cfg); }

        public string LabelFor(string id, Config c)
        {
            string v;
            if (id != null && c.BubbleLabels.TryGetValue(id, out v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            ActionDef a;
            return id != null && Actions.TryGetValue(id, out a) ? a.Label : (id ?? "");
        }

        public Color AccentColor
        {
            get
            {
                try { return (Color)ColorConverter.ConvertFromString(Cfg.Accent); }
                catch (Exception) { return Color.FromRgb(0xD9, 0x77, 0x57); }
            }
        }

        public Color ColorFor(string id) { return ColorFor(id, Cfg); }

        public Color ColorFor(string id, Config c)
        {
            string v;
            if (id != null && c.BubbleColors.TryGetValue(id, out v) && !string.IsNullOrEmpty(v))
            {
                try { return (Color)ColorConverter.ConvertFromString(v); } catch (Exception) { }
            }
            try { return (Color)ColorConverter.ConvertFromString(c.Accent); }
            catch (Exception) { return Color.FromRgb(0xD9, 0x77, 0x57); }
        }

        public double UiScaleOf(Config c) { return Math.Max(0.8, Math.Min(1.5, c.UiScale)); }

        public double UiScale
        {
            get { return Math.Max(0.8, Math.Min(1.5, Cfg.UiScale)); }
        }

        public bool ApplyHotkey(string hotkey)
        {
            Native.UnregisterHotKey(hwnd, 1);
            uint mods, vk;
            bool ok = Native.ParseHotkey(hotkey, out mods, out vk) && Native.RegisterHotKey(hwnd, 1, mods | 0x4000, vk);
            string tip = "Claude 액션링 (" + hotkey + ")";
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            return ok;
        }

        public void ShowRingCenter()
        {
            ring.ShowAtScreenCenter();
        }

        // 설정 창은 하나뿐: section = null(일반) | "ring" | "slots" | "slot:3" | 기능 id(예: "everything")
        public void OpenSettings(string section)
        {
            ring.HideRing();
            try { GeneralSettingsWindow.OpenAt(this, section); }
            catch (Exception ex) { Log("설정 오류: " + ex); Toast("설정을 열지 못했어요: " + ex.Message, 5000); }
        }

        public void OpenGeneralSettings() { OpenSettings(null); }

        public void OpenSearchSettings() { OpenSettings("everything"); }

        public void OpenConfig()
        {
            ring.HideRing();
            try
            {
                if (!IO.File.Exists(Config.FilePath)) Cfg.Save();
                Process.Start("notepad.exe", "\"" + Config.FilePath + "\"");
            }
            catch (Exception ex)
            {
                Toast("설정 파일을 열지 못했어요: " + ex.Message, 4000);
            }
        }

        public void Quit()
        {
            ring.HideRing();
            Native.UnregisterHotKey(hwnd, 1);
            tray.Visible = false;
            tray.Dispose();
            App.Shutdown();
        }

        // ----- 모니터 기준 위치 계산 (여러 모니터 지원) -----
        public System.Windows.Rect WorkAreaAt(System.Windows.Point dip)
        {
            try
            {
                double sc = Native.DpiScale();
                var pt = new System.Drawing.Point((int)Math.Round(dip.X * sc), (int)Math.Round(dip.Y * sc));
                var wa = WinForms.Screen.FromPoint(pt).WorkingArea;
                return new System.Windows.Rect(wa.Left / sc, wa.Top / sc, wa.Width / sc, wa.Height / sc);
            }
            catch (Exception) { return SystemParameters.WorkArea; }
        }

        public System.Windows.Point CursorDip()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            double sc = Native.DpiScale();
            return new System.Windows.Point(p.X / sc, p.Y / sc);
        }

        // 창이 center 모니터 안에 들어오도록 왼쪽 위 좌표를 구함
        public System.Windows.Point PlaceInMonitor(System.Windows.Point center, double w, double h)
        {
            var a = WorkAreaAt(center);
            double left = center.X - w / 2, top = center.Y - h / 2;
            left = Math.Max(a.Left, Math.Min(left, a.Right - w));
            top = Math.Max(a.Top, Math.Min(top, a.Bottom - h));
            return new System.Windows.Point(left, top);
        }

        // 화면들을 왼쪽→오른쪽 순서로 (1 모니터, 2 모니터 …)
        public WinForms.Screen[] ScreensOrdered()
        {
            var arr = WinForms.Screen.AllScreens.ToList();
            arr.Sort((a, b) => a.Bounds.X != b.Bounds.X ? a.Bounds.X.CompareTo(b.Bounds.X) : a.Bounds.Y.CompareTo(b.Bounds.Y));
            return arr.ToArray();
        }

        public int ScreenCount { get { try { return ScreensOrdered().Length; } catch (Exception) { return 1; } } }

        // 설정한 모니터의 작업 영역 (window_monitor = cursor면 커서가 있는 모니터)
        public System.Windows.Rect TargetWorkArea(System.Windows.Point cursor)
        {
            try
            {
                double sc = Native.DpiScale();
                string m = Cfg.WindowMonitor ?? "cursor";
                WinForms.Screen scr;
                int idx;
                if (m != "cursor" && int.TryParse(m, out idx))
                {
                    var arr = ScreensOrdered();
                    scr = arr[Math.Max(0, Math.Min(arr.Length - 1, idx - 1))];
                }
                else
                {
                    var pt = new System.Drawing.Point((int)Math.Round(cursor.X * sc), (int)Math.Round(cursor.Y * sc));
                    scr = WinForms.Screen.FromPoint(pt);
                }
                var wa = scr.WorkingArea;
                return new System.Windows.Rect(wa.Left / sc, wa.Top / sc, wa.Width / sc, wa.Height / sc);
            }
            catch (Exception) { return SystemParameters.WorkArea; }
        }

        public System.Windows.Point CornerPlace(System.Windows.Rect a, double w, double h, string corner)
        {
            double m = 24, left, top;
            if (corner == "topleft") { left = a.Left + m; top = a.Top + m; }
            else if (corner == "topright") { left = a.Right - w - m; top = a.Top + m; }
            else if (corner == "bottomleft") { left = a.Left + m; top = a.Bottom - h - m; }
            else if (corner == "bottomright") { left = a.Right - w - m; top = a.Bottom - h - m; }
            else { left = a.Left + (a.Width - w) / 2; top = a.Top + (a.Height - h) / 2; }
            left = Math.Max(a.Left, Math.Min(left, a.Right - w));
            top = Math.Max(a.Top, Math.Min(top, a.Bottom - h));
            return new System.Windows.Point(left, top);
        }

        // 기능 창(검색·물어보기·정리·설정)이 뜰 왼쪽 위 좌표
        public System.Windows.Point WindowTopLeft(double w, double h)
        {
            var area = TargetWorkArea(LastPoint);
            string corner = Cfg.WindowCorner ?? "cursor";
            bool followCursor = (Cfg.WindowMonitor ?? "cursor") == "cursor";
            if (corner == "cursor")
            {
                if (followCursor)
                {
                    double left = LastPoint.X - w / 2, top = LastPoint.Y - h / 2;
                    left = Math.Max(area.Left, Math.Min(left, area.Right - w));
                    top = Math.Max(area.Top, Math.Min(top, area.Bottom - h));
                    return new System.Windows.Point(left, top);
                }
                corner = "center"; // 특정 모니터 지정 시 마우스 근처는 가운데로
            }
            return CornerPlace(area, w, h, corner);
        }

        // 설정한 모니터의 WinForms.Screen (물리 픽셀)
        public WinForms.Screen TargetScreen(System.Windows.Point cursorDip)
        {
            try
            {
                string m = Cfg.WindowMonitor ?? "cursor";
                int idx;
                if (m != "cursor" && int.TryParse(m, out idx))
                {
                    var arr = ScreensOrdered();
                    return arr[Math.Max(0, Math.Min(arr.Length - 1, idx - 1))];
                }
                double sc = Native.DpiScale();
                var pt = new System.Drawing.Point((int)Math.Round(cursorDip.X * sc), (int)Math.Round(cursorDip.Y * sc));
                return WinForms.Screen.FromPoint(pt);
            }
            catch (Exception) { return WinForms.Screen.PrimaryScreen; }
        }

        // 메모장·스티커 메모 같은 외부 창을 설정한 모니터/모서리로 옮김
        public void PlaceExternalWindow(IntPtr h)
        {
            try
            {
                if (h == IntPtr.Zero) return;
                Native.RECT r;
                if (!Native.GetWindowRect(h, out r)) return;
                int w = r.right - r.left, ht = r.bottom - r.top;
                if (w <= 0 || ht <= 0) return;
                var wa = TargetScreen(CursorDip()).WorkingArea;
                double sc = Native.DpiScale();
                int m = (int)Math.Round(24 * sc);
                string corner = Cfg.WindowCorner ?? "cursor";
                int left, top;
                if (corner == "topleft") { left = wa.Left + m; top = wa.Top + m; }
                else if (corner == "topright") { left = wa.Right - w - m; top = wa.Top + m; }
                else if (corner == "bottomleft") { left = wa.Left + m; top = wa.Bottom - ht - m; }
                else if (corner == "bottomright") { left = wa.Right - w - m; top = wa.Bottom - ht - m; }
                else if (corner == "cursor" && (Cfg.WindowMonitor ?? "cursor") == "cursor")
                {
                    Native.POINT cp; Native.GetCursorPos(out cp);
                    left = cp.X - w / 2; top = cp.Y - ht / 2;
                }
                else { left = wa.Left + (wa.Width - w) / 2; top = wa.Top + (wa.Height - ht) / 2; }
                left = Math.Max(wa.Left, Math.Min(left, wa.Right - w));
                top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - ht));
                Native.SetWindowPos(h, IntPtr.Zero, left, top, 0, 0, 0x0001 | 0x0004); // NOSIZE | NOZORDER
            }
            catch (Exception ex) { Log("메모 창 위치 조정 실패: " + ex.Message); }
        }

        public void Toast(string text, int ms)
        {
            var w = new Window();
            w.WindowStyle = WindowStyle.None;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.Topmost = true;
            w.ShowInTaskbar = false;
            w.ShowActivated = false;
            w.SizeToContent = SizeToContent.WidthAndHeight;
            w.IsHitTestVisible = false;
            var border = new Border();
            border.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x13));
            border.CornerRadius = new CornerRadius(20);
            border.Padding = new Thickness(18, 10, 18, 10);
            border.Margin = new Thickness(12);
            border.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.3, Direction = 270 };
            var tb = new TextBlock();
            tb.Text = text;
            tb.Foreground = new SolidColorBrush(Color.FromRgb(0xFA, 0xF9, 0xF5));
            tb.FontFamily = Theme.Font;
            tb.FontSize = 13;
            border.Child = tb;
            border.LayoutTransform = new ScaleTransform(UiScale, UiScale);
            w.Content = border;
            System.Windows.Point tc = CursorDip();
            w.Loaded += (s, e) =>
            {
                var wa = WorkAreaAt(tc);
                w.Left = wa.Left + (wa.Width - w.ActualWidth) / 2;
                w.Top = wa.Bottom - w.ActualHeight - 40;
            };
            w.Show();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            timer.Tick += (s, e) => { timer.Stop(); w.Close(); };
            timer.Start();
        }
    }

    // ------------------------------------------------------------ 링 창
    class RingWindow : Window
    {
        const double BUBBLE = 64;
        const double CORE = 92;
        const double LABEL_GAP = 62;
        static readonly Color IVORY = Color.FromRgb(0xFA, 0xF9, 0xF5);
        static readonly Color INK = Color.FromRgb(0x14, 0x14, 0x13);
        static readonly Color PILL = Color.FromArgb(0xDD, 0x2B, 0x2A, 0x27);
        static readonly Color EMPTY_LINE = Color.FromArgb(0xB0, 0xFA, 0xF9, 0xF5);
        static readonly Color EMPTY_FILL = Color.FromArgb(0x30, 0xFA, 0xF9, 0xF5);

        RingApp app;
        Canvas root;
        double cx, cy, R;
        Color accent;
        string[] slots;
        DateTime start;
        bool running;
        int hover = -1; // 0~8 = 칸, 9 = 코어, 10 = 설정
        double speed = 1;
        Grid gear;
        Grid gearBadge;
        ScaleTransform gearScale;
        Shapes.Ellipse gearCircle;
        TextBlock gearIcon;

        Shapes.Ellipse guide;
        Shapes.Ellipse[] ripples = new Shapes.Ellipse[3];
        Shapes.Line[] spokes = new Shapes.Line[9];
        Grid[] bubbles = new Grid[9];
        ScaleTransform[] bubbleScale = new ScaleTransform[9];
        Shapes.Ellipse[] bubbleCircle = new Shapes.Ellipse[9];
        TextBlock[] bubbleIcon = new TextBlock[9];
        SolidColorBrush[] bubbleBrush = new SolidColorBrush[9];
        Border[] labels = new Border[9];
        TextBlock[] labelText = new TextBlock[9];
        Grid core;
        ScaleTransform coreScale;
        TextBlock coreLabel;

        public RingWindow(RingApp owner)
        {
            app = owner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Background = new SolidColorBrush(Color.FromArgb(0x4D, 0, 0, 0)); // 화면을 살짝 어둡게
            root = new Canvas();
            Content = root;

            KeyDown += (s, e) => { if (e.Key == Key.Escape) HideRing(); };
            MouseLeftButtonUp += (s, e) => { if (!e.Handled) HideRing(); };
            Deactivated += (s, e) => HideRing();

            // 우클릭: 버블 위면 그 버블 설정, 그 밖이면 일반 설정
            MouseRightButtonUp += (s, e) => { if (!e.Handled) { e.Handled = true; app.OpenSettings(null); } };
        }

        // ----- 열기/닫기
        public void ShowAtCursor()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            double s = Native.DpiScale();
            OpenAt(p.X / s, p.Y / s);
        }

        public void ShowAtScreenCenter()
        {
            var wa = app.WorkAreaAt(app.CursorDip());
            OpenAt(wa.Left + wa.Width / 2, wa.Top + wa.Height / 2);
        }

        void OpenAt(double x, double y)
        {
            if (IsVisible) HideRing();
            app.LastPoint = new Point(x, y);
            app.Cfg = Config.Load();
            Theme.Load(app.Cfg);
            R = app.Cfg.Radius;
            slots = app.Cfg.Slots;
            try { accent = (Color)ColorConverter.ConvertFromString(app.Cfg.Accent); }
            catch (Exception) { accent = Color.FromRgb(0xD9, 0x77, 0x57); }

            Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Max(0, Math.Min(60, app.Cfg.DimPercent)) / 100.0), 0, 0, 0));
            speed = app.Cfg.Animation == "fast" ? 1.7 : 1.0;
            bool animate = app.Cfg.Animation != "off";

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            cx = x - Left;
            cy = y - Top;

            hover = -1;
            Build();
            UpdateFrame(animate ? 0 : 5000);
            Show();
            Activate();
            Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);
            Focus();

            start = DateTime.Now;
            if (animate && !running)
            {
                running = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        public void HideRing()
        {
            if (running)
            {
                CompositionTarget.Rendering -= OnRendering;
                running = false;
            }
            if (IsVisible) Hide();
        }

        void OnRendering(object sender, EventArgs e)
        {
            double t = (DateTime.Now - start).TotalMilliseconds * speed;
            UpdateFrame(t);
            if (t > 1100)
            {
                CompositionTarget.Rendering -= OnRendering;
                running = false;
            }
        }

        bool IsEmpty(int i)
        {
            return slots[i] == null || !app.Actions.ContainsKey(slots[i]);
        }

        Point SlotCenter(int i)
        {
            double a = (-90 + i * 40) * Math.PI / 180;
            return new Point(cx + Math.Cos(a) * R, cy + Math.Sin(a) * R);
        }

        // ----- 요소 만들기
        void Build()
        {
            root.Children.Clear();
            var accentBrush = new SolidColorBrush(accent);

            guide = new Shapes.Ellipse();
            guide.Width = guide.Height = 2 * R;
            guide.Stroke = new SolidColorBrush(Color.FromArgb(0x70, 0xFA, 0xF9, 0xF5));
            guide.StrokeThickness = 1.5;
            guide.StrokeDashArray = new DoubleCollection(new double[] { 2, 3 });
            guide.IsHitTestVisible = false;
            Canvas.SetLeft(guide, cx - R);
            Canvas.SetTop(guide, cy - R);
            root.Children.Add(guide);

            for (int k = 0; k < 3; k++)
            {
                var e = new Shapes.Ellipse();
                e.Stroke = accentBrush;
                e.IsHitTestVisible = false;
                ripples[k] = e;
                root.Children.Add(e);
            }

            for (int i = 0; i < 9; i++)
            {
                var line = new Shapes.Line();
                line.Stroke = accentBrush;
                line.StrokeThickness = 2;
                line.StrokeStartLineCap = PenLineCap.Round;
                line.StrokeEndLineCap = PenLineCap.Round;
                line.IsHitTestVisible = false;
                spokes[i] = line;
                root.Children.Add(line);
            }

            for (int i = 0; i < 9; i++)
            {
                int idx = i; // C# 5: for 변수는 람다에서 복사해서 사용
                Point c = SlotCenter(i);
                bool empty = IsEmpty(i);

                var g = new Grid();
                g.Width = g.Height = BUBBLE;
                g.Cursor = Cursors.Hand;
                g.RenderTransformOrigin = new Point(0.5, 0.5);
                var sc = new ScaleTransform(0, 0);
                g.RenderTransform = sc;
                Canvas.SetLeft(g, c.X - BUBBLE / 2);
                Canvas.SetTop(g, c.Y - BUBBLE / 2);

                var circle = new Shapes.Ellipse();
                var icon = new TextBlock();
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                icon.VerticalAlignment = VerticalAlignment.Center;
                icon.IsHitTestVisible = false;
                if (empty)
                {
                    circle.Fill = new SolidColorBrush(EMPTY_FILL);
                    circle.Stroke = new SolidColorBrush(EMPTY_LINE);
                    circle.StrokeThickness = 2;
                    circle.StrokeDashArray = new DoubleCollection(new double[] { 3, 2 });
                    icon.Text = "+";
                    icon.FontFamily = Theme.Font;
                    icon.FontSize = 24;
                    icon.Margin = new Thickness(0, 0, 0, 3);
                    icon.Foreground = new SolidColorBrush(EMPTY_LINE);
                }
                else
                {
                    circle.Fill = Brushes.White;
                    g.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.35, Direction = 270 };
                    bubbleBrush[i] = new SolidColorBrush(app.ColorFor(slots[i]));
                    icon.Text = app.Actions[slots[i]].Icon;
                    icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                    icon.FontSize = 24;
                    icon.Foreground = bubbleBrush[i];
                }
                g.Children.Add(circle);
                g.Children.Add(icon);

                g.MouseEnter += (s, e) => SetHover(idx);
                g.MouseLeave += (s, e) => { if (hover == idx) SetHover(-1); };
                g.MouseLeftButtonUp += (s, e) => { e.Handled = true; ClickSlot(idx); };
                g.MouseRightButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    app.OpenSettings(IsEmpty(idx) ? "slot:" + idx : slots[idx]);
                };
                g.ToolTip = empty ? "빈 칸 · 우클릭: 이 칸에 기능 넣기" : "클릭: 실행 · 우클릭: " + app.LabelFor(slots[i]) + " 설정";

                bubbles[i] = g; bubbleScale[i] = sc; bubbleCircle[i] = circle; bubbleIcon[i] = icon;
                root.Children.Add(g);

                labels[i] = null;
                if (!empty)
                {
                    var lb = new Border();
                    lb.CornerRadius = new CornerRadius(12);
                    lb.Padding = new Thickness(10, 3, 10, 4);
                    lb.IsHitTestVisible = false;
                    var lt = new TextBlock();
                    lt.Text = app.LabelFor(slots[i]);
                    lt.FontFamily = Theme.Font;
                    lt.FontSize = 13 * app.UiScale;
                    lb.Child = lt;
                    lb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double a = (-90 + i * 40) * Math.PI / 180;
                    double lx = cx + Math.Cos(a) * (R + LABEL_GAP);
                    double ly = cy + Math.Sin(a) * (R + LABEL_GAP);
                    Canvas.SetLeft(lb, lx - lb.DesiredSize.Width / 2);
                    Canvas.SetTop(lb, ly - lb.DesiredSize.Height / 2);
                    lb.Opacity = 0;
                    labels[i] = lb; labelText[i] = lt;
                    root.Children.Add(lb);
                }
            }

            core = new Grid();
            core.Width = core.Height = CORE;
            core.Cursor = Cursors.Hand;
            core.RenderTransformOrigin = new Point(0.5, 0.5);
            coreScale = new ScaleTransform(0, 0);
            core.RenderTransform = coreScale;
            core.Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 5, Opacity = 0.4, Direction = 270 };
            Canvas.SetLeft(core, cx - CORE / 2);
            Canvas.SetTop(core, cy - CORE / 2);
            var coreCircle = new Shapes.Ellipse();
            coreCircle.Fill = accentBrush;
            coreCircle.Stroke = new SolidColorBrush(IVORY);
            coreCircle.StrokeThickness = 3;
            core.Children.Add(coreCircle);
            var stack = new StackPanel();
            stack.HorizontalAlignment = HorizontalAlignment.Center;
            stack.VerticalAlignment = VerticalAlignment.Center;
            stack.IsHitTestVisible = false;
            var star = new Shapes.Path();
            star.Data = Geometry.Parse("M12,2.5 C12.5,7.3 15.7,10.5 20.5,11 L20.5,13 C15.7,13.5 12.5,16.7 12,21.5 L10,21.5 C9.5,16.7 6.3,13.5 1.5,13 L1.5,11 C6.3,10.5 9.5,7.3 10,2.5 Z");
            star.Fill = Brushes.White;
            star.Width = 26; star.Height = 26;
            star.Stretch = Stretch.Uniform;
            star.HorizontalAlignment = HorizontalAlignment.Center;
            coreLabel = new TextBlock();
            coreLabel.Foreground = Brushes.White;
            coreLabel.FontFamily = Theme.Font;
            coreLabel.FontSize = 12;
            coreLabel.FontWeight = FontWeights.Bold;
            coreLabel.HorizontalAlignment = HorizontalAlignment.Center;
            coreLabel.Margin = new Thickness(0, 2, 0, 0);
            stack.Children.Add(star);
            stack.Children.Add(coreLabel);
            core.Children.Add(stack);
            core.MouseEnter += (s, e) => SetHover(9);
            core.MouseLeave += (s, e) => { if (hover == 9) SetHover(-1); };
            core.ToolTip = "클릭: Claude 열기 · 우클릭: 링 설정";
            core.MouseRightButtonUp += (s, e) => { e.Handled = true; app.OpenSettings("ring"); };
            core.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                HideRing();
                app.OpenClaude();
            };
            root.Children.Add(core);

            // 코어 옆 작은 설정 버튼
            gear = new Grid();
            gear.Width = gear.Height = 30;
            gear.Cursor = Cursors.Hand;
            gear.ToolTip = "전체 설정";
            gear.RenderTransformOrigin = new Point(0.5, 0.5);
            gearScale = new ScaleTransform(0, 0);
            gear.RenderTransform = gearScale;
            gear.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.35, Direction = 270 };
            Canvas.SetLeft(gear, cx + 26);
            Canvas.SetTop(gear, cy + 26);
            gearCircle = new Shapes.Ellipse();
            gearCircle.Fill = Brushes.White;
            gearCircle.Stroke = accentBrush;
            gearCircle.StrokeThickness = 1.5;
            gearIcon = new TextBlock();
            gearIcon.Text = "\uE713";
            gearIcon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            gearIcon.FontSize = 14;
            gearIcon.Foreground = accentBrush;
            gearIcon.HorizontalAlignment = HorizontalAlignment.Center;
            gearIcon.VerticalAlignment = VerticalAlignment.Center;
            gearIcon.IsHitTestVisible = false;
            gear.Children.Add(gearCircle);
            gear.Children.Add(gearIcon);
            // 업데이트 알림 배지 ("!")
            gearBadge = new Grid { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -2, -2, 0), IsHitTestVisible = false };
            gearBadge.Children.Add(new Shapes.Ellipse { Fill = new SolidColorBrush(Color.FromRgb(0xE1, 0x1D, 0x2E)), Stroke = Brushes.White, StrokeThickness = 1.5 });
            gearBadge.Children.Add(new TextBlock { Text = "!", Foreground = Brushes.White, FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            gearBadge.Visibility = app.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
            gear.Children.Add(gearBadge);
            gear.ToolTip = app.UpdateAvailable ? "새 버전 v" + app.UpdateVersion + " · 눌러서 업데이트" : "전체 설정";
            gear.MouseEnter += (s, e) => SetHover(10);
            gear.MouseLeave += (s, e) => { if (hover == 10) SetHover(-1); };
            gear.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                HideRing();
                if (app.UpdateAvailable) app.PromptUpdate();
                else app.OpenGeneralSettings();
            };
            root.Children.Add(gear);
        }

        // ----- 애니메이션 한 프레임
        static double Clamp01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }
        static double EaseOutCubic(double t) { return 1 - Math.Pow(1 - t, 3); }
        static double EaseOutBack(double t)
        {
            const double c1 = 1.70158, c3 = c1 + 1;
            return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
        }

        void UpdateFrame(double t)
        {
            guide.Opacity = Clamp01((t - 200) / 300);

            for (int k = 0; k < 3; k++)
            {
                double tt = (t - k * 110) / 720;
                var e = ripples[k];
                if (tt < 0 || tt >= 1) { e.Opacity = 0; continue; }
                double rk = (2 * R + 40 - 46 * k) / 2;
                double size = 2 * rk * (0.15 + 1.1 * EaseOutCubic(tt));
                e.Width = e.Height = size;
                Canvas.SetLeft(e, cx - size / 2);
                Canvas.SetTop(e, cy - size / 2);
                e.StrokeThickness = Math.Max(0.8, (3 - k) * (1 - tt));
                e.Opacity = 0.9 * (1 - tt);
            }

            for (int i = 0; i < 9; i++)
            {
                double tt = Clamp01((t - (70 + i * 16)) / 520);
                double grow = EaseOutCubic(Math.Min(tt / 0.55, 1));
                double fade = tt < 0.55 ? 0 : (tt - 0.55) / 0.45;
                double a = (-90 + i * 40) * Math.PI / 180;
                double r0 = CORE / 2 + 4;
                double r1 = r0 + (R - BUBBLE / 2 - r0) * grow;
                var l = spokes[i];
                l.X1 = cx + Math.Cos(a) * r0; l.Y1 = cy + Math.Sin(a) * r0;
                l.X2 = cx + Math.Cos(a) * r1; l.Y2 = cy + Math.Sin(a) * r1;
                l.Opacity = tt <= 0 ? 0 : 1 - (IsEmpty(i) ? 0.8 : 0.7) * fade;
            }

            for (int i = 0; i < 9; i++)
            {
                double tt = Clamp01((t - (170 + i * 40)) / 440);
                double s = tt <= 0 ? 0 : EaseOutBack(tt);
                if (hover == i) s *= 1.14;
                bubbleScale[i].ScaleX = bubbleScale[i].ScaleY = s;
                if (labels[i] != null) labels[i].Opacity = Clamp01((t - (380 + i * 40)) / 200);
            }

            double cs = EaseOutBack(Clamp01(t / 300)) * (hover == 9 ? 1.05 : 1);
            coreScale.ScaleX = coreScale.ScaleY = cs;
            double gs = EaseOutBack(Clamp01((t - 250) / 300)) * (hover == 10 ? 1.12 : 1);
            gearScale.ScaleX = gearScale.ScaleY = gs;

            ApplyHoverColors();
        }

        void ApplyHoverColors()
        {
            var accentBrush = new SolidColorBrush(accent);
            for (int i = 0; i < 9; i++)
            {
                bool lit = hover == i;
                if (IsEmpty(i))
                {
                    bubbleCircle[i].Stroke = lit ? accentBrush : new SolidColorBrush(EMPTY_LINE);
                    bubbleIcon[i].Foreground = lit ? new SolidColorBrush(IVORY) : new SolidColorBrush(EMPTY_LINE);
                }
                else
                {
                    bubbleCircle[i].Fill = lit ? bubbleBrush[i] : Brushes.White;
                    bubbleIcon[i].Foreground = lit ? Brushes.White : bubbleBrush[i];
                    labels[i].Background = new SolidColorBrush(lit ? IVORY : PILL);
                    labelText[i].Foreground = new SolidColorBrush(lit ? INK : IVORY);
                    labelText[i].FontWeight = lit ? FontWeights.Bold : FontWeights.Normal;
                }
            }

            string text = "Claude";
            if (hover >= 0 && hover < 9) text = IsEmpty(hover) ? "빈 칸" : app.LabelFor(slots[hover]);
            else if (hover == 9) text = "열기";
            else if (hover == 10) text = "설정";
            gearCircle.Fill = hover == 10 ? accentBrush : Brushes.White;
            gearIcon.Foreground = hover == 10 ? Brushes.White : accentBrush;
            coreLabel.Text = text;
        }

        void SetHover(int h)
        {
            if (hover == h) return;
            hover = h;
            if (!running) UpdateFrame(5000);
        }

        void ClickSlot(int i)
        {
            if (IsEmpty(i))
            {
                app.Toast("빈 칸이에요 · 설정 파일의 slots에 기능을 넣을 수 있어요", 2400);
                return;
            }
            string id = slots[i];
            HideRing();
            Dispatcher.BeginInvoke(new Action(() => app.RunAction(id)), DispatcherPriority.Background);
        }
    }

    // ------------------------------------------------------------ 기능 1: 파일 검색 (자체 색인)
    struct IndexEntry
    {
        public int Dir;
        public string Name;
        public bool IsDir;
    }

    class IndexSnapshot
    {
        public string[] Dirs;
        public IndexEntry[] Entries;
        public DateTime Built;
        public bool Complete;
    }

    class Hit
    {
        public string Dir;
        public string Name;
        public bool IsDir;
        public bool Remote;
        public int Score;
        public long Size = -1;
        public DateTime Modified;
        public string FullPath { get { return Dir + "\\" + Name; } }
    }

    // 색인 하나 (내 PC 드라이브 / 네트워크 드라이브 / 설정한 폴더마다 하나씩)
    class IndexSource
    {
        public string Key;
        public string Label;
        public string CacheFile;
        public bool Watch;
        public bool Subfolders = true;
        public string Mode = "interval";          // daily | interval | never
        public TimeSpan DailyTime = TimeSpan.FromHours(3);
        public double IntervalHours = 1;
        public TimeSpan RetryWhenOffline = TimeSpan.FromMinutes(10);
        public Func<string[]> RootsFn;

        public volatile IndexSnapshot Current = new IndexSnapshot { Dirs = new string[0], Entries = new IndexEntry[0], Built = DateTime.MinValue, Complete = false };
        public volatile bool Scanning;
        public volatile int ScanCount;
        public volatile string[] Roots = new string[0];
        public volatile bool Offline;
        public volatile string LastError = "";
        public readonly AutoResetEvent Wake = new AutoResetEvent(false);
        volatile bool stopped;
        volatile bool force;
        readonly List<IO.FileSystemWatcher> watchers = new List<IO.FileSystemWatcher>();

        string CachePath { get { return IO.Path.Combine(Config.Dir, CacheFile); } }

        public void Start()
        {
            var t = new Thread(Loop);
            t.IsBackground = true;
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();
        }

        public void Stop()
        {
            stopped = true;
            Wake.Set();
        }

        public void RequestScan()
        {
            force = true;
            Wake.Set();
        }

        DateTime LastDaily()
        {
            DateTime today = DateTime.Today + DailyTime;
            return DateTime.Now >= today ? today : today.AddDays(-1);
        }

        bool Due()
        {
            var built = Current.Built;
            if (built == DateTime.MinValue) return true;   // 아직 한 번도 색인 안 함
            if (Mode == "never") return false;
            if (Mode == "daily") return built < LastDaily();
            return DateTime.Now - built >= TimeSpan.FromHours(IntervalHours);
        }

        TimeSpan NextWait()
        {
            TimeSpan w;
            if (Mode == "never") w = TimeSpan.FromMinutes(30);
            else if (Mode == "daily") w = LastDaily().AddDays(1) - DateTime.Now;
            else w = Current.Built.AddHours(IntervalHours) - DateTime.Now;
            if (w < TimeSpan.FromMinutes(1)) w = TimeSpan.FromMinutes(1);
            if (w > TimeSpan.FromMinutes(30)) w = TimeSpan.FromMinutes(30); // 30분마다 일정 다시 확인
            return w;
        }

        void Loop()
        {
            try { LoadCache(); } catch (Exception) { }
            while (!stopped)
            {
                TimeSpan wait;
                try
                {
                    string[] all = RootsFn();
                    string[] ok = all.Where(r => { try { return IO.Directory.Exists(r); } catch (Exception) { return false; } }).ToArray();
                    Roots = all;
                    Offline = all.Length > 0 && ok.Length < all.Length;
                    if (all.Length > 0 && !Offline && Watch && watchers.Count == 0) StartWatchers(all);
                    bool doScan = all.Length > 0 && !Offline && (force || Due());
                    force = false;
                    if (doScan) Scan(all);
                    wait = Offline ? RetryWhenOffline : NextWait();
                }
                catch (Exception)
                {
                    Scanning = false;
                    wait = RetryWhenOffline;
                }
                if (stopped) break;
                Wake.WaitOne(wait);
            }
            StopWatchers();
        }

        void Scan(string[] roots)
        {
            Scanning = true;
            ScanCount = 0;
            DateTime scanStart = DateTime.Now;
            bool firstEver = Current.Entries.Length == 0;
            int nextPublish = 30000;
            var dirs = new List<string>();
            var entries = new List<IndexEntry>(1 << 16);
            var stack = new Stack<string>();
            var rootSet = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
            for (int i = roots.Length - 1; i >= 0; i--) stack.Push(roots[i]);

            while (stack.Count > 0 && !stopped)
            {
                string d = stack.Pop();
                if (FileIndex.IsExcluded(d)) continue;
                int di = dirs.Count;
                dirs.Add(d.TrimEnd('\\'));
                bool descend = Subfolders || rootSet.Contains(d);
                try
                {
                    foreach (var fsi in new IO.DirectoryInfo(d).EnumerateFileSystemInfos())
                    {
                        var a = fsi.Attributes;
                        if ((a & IO.FileAttributes.Hidden) != 0 && (a & IO.FileAttributes.System) != 0) continue;
                        bool isDir = (a & IO.FileAttributes.Directory) != 0;
                        if (isDir)
                        {
                            if ((a & IO.FileAttributes.ReparsePoint) != 0) continue;
                            if (FileIndex.IsExcludedName(fsi.Name)) continue;
                            if (Subfolders) stack.Push(fsi.FullName);
                        }
                        entries.Add(new IndexEntry { Dir = di, Name = fsi.Name, IsDir = isDir });
                    }
                }
                catch (Exception)
                {
                    // 권한 없는 폴더, 너무 긴 경로 등은 건너뜀
                }
                ScanCount = entries.Count;
                if (firstEver && entries.Count >= nextPublish)
                {
                    Current = new IndexSnapshot { Dirs = dirs.ToArray(), Entries = entries.ToArray(), Built = DateTime.MinValue, Complete = false };
                    nextPublish += 150000;
                }
            }
            if (stopped) { Scanning = false; return; }

            Current = new IndexSnapshot { Dirs = dirs.ToArray(), Entries = entries.ToArray(), Built = DateTime.Now, Complete = true };
            FileIndex.TrimOverlay(scanStart, roots);
            Scanning = false;
            try { SaveCache(); } catch (Exception) { }
        }

        void SaveCache()
        {
            var snap = Current;
            IO.Directory.CreateDirectory(Config.Dir);
            string tmp = CachePath + ".tmp";
            using (var fs = new IO.FileStream(tmp, IO.FileMode.Create))
            using (var bw = new IO.BinaryWriter(new IO.BufferedStream(fs, 1 << 16), Encoding.UTF8))
            {
                bw.Write("CRIX1");
                bw.Write(snap.Dirs.Length);
                foreach (var d in snap.Dirs) bw.Write(d);
                bw.Write(snap.Entries.Length);
                foreach (var e in snap.Entries) { bw.Write(e.Dir); bw.Write(e.Name); bw.Write(e.IsDir); }
            }
            if (IO.File.Exists(CachePath)) IO.File.Delete(CachePath);
            IO.File.Move(tmp, CachePath);
        }

        void LoadCache()
        {
            if (Current.Entries.Length > 0 || !IO.File.Exists(CachePath)) return;
            using (var fs = new IO.FileStream(CachePath, IO.FileMode.Open, IO.FileAccess.Read))
            using (var br = new IO.BinaryReader(new IO.BufferedStream(fs, 1 << 16), Encoding.UTF8))
            {
                if (br.ReadString() != "CRIX1") return;
                int nd = br.ReadInt32();
                var dirs = new string[nd];
                for (int i = 0; i < nd; i++) dirs[i] = br.ReadString();
                int ne = br.ReadInt32();
                var entries = new IndexEntry[ne];
                for (int i = 0; i < ne; i++)
                {
                    entries[i].Dir = br.ReadInt32();
                    entries[i].Name = br.ReadString();
                    entries[i].IsDir = br.ReadBoolean();
                }
                if (Current.Entries.Length == 0)
                    Current = new IndexSnapshot { Dirs = dirs, Entries = entries, Built = IO.File.GetLastWriteTime(CachePath), Complete = true };
            }
        }

        void StartWatchers(string[] roots)
        {
            foreach (var r in roots)
            {
                try
                {
                    var w = new IO.FileSystemWatcher(r);
                    w.IncludeSubdirectories = Subfolders;
                    w.NotifyFilter = IO.NotifyFilters.FileName | IO.NotifyFilters.DirectoryName;
                    w.InternalBufferSize = 65536;
                    w.Created += (s, e) => FileIndex.AddOverlay(e.FullPath);
                    w.Renamed += (s, e) => FileIndex.AddOverlay(e.FullPath);
                    w.EnableRaisingEvents = true;
                    watchers.Add(w);
                }
                catch (Exception) { }
            }
        }

        void StopWatchers()
        {
            foreach (var w in watchers)
            {
                try { w.EnableRaisingEvents = false; w.Dispose(); } catch (Exception) { }
            }
            watchers.Clear();
        }

        public string ScheduleText()
        {
            if (Mode == "never") return "재스캔 안 함";
            if (Mode == "daily") return "매일 " + new DateTime(2000, 1, 1).Add(DailyTime).ToString("HH:mm");
            return IntervalHours < 1 ? ((int)(IntervalHours * 60)) + "분마다" : IntervalHours.ToString("0.#") + "시간마다";
        }

        public string StatusLine()
        {
            var snap = Current;
            if (Scanning) return "색인 중… " + ScanCount.ToString("N0") + "개";
            string count = snap.Entries.Length.ToString("N0") + "개";
            string when = snap.Built == DateTime.MinValue ? "" : " · " + snap.Built.ToString("MM-dd HH:mm") + " 색인";
            if (Offline) return "연결 안 됨" + (LastError.Length > 0 ? " · " + LastError : "") + (snap.Entries.Length > 0 ? " · 마지막 색인 " + count + when : "");
            if (Roots.Length == 0) return "대상 없음";
            return count + when + " · " + ScheduleText() + (Watch ? " · 변경 감시" : "");
        }
    }

    static class FileIndex
    {
        public static volatile IndexSource[] Sources = new IndexSource[0];

        static readonly object gate = new object();
        static readonly object overlayGate = new object();
        static List<KeyValuePair<DateTime, string>> overlay = new List<KeyValuePair<DateTime, string>>();
        static volatile string[] excludePrefixes = new string[0];
        static volatile HashSet<string> excludeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Start(Config cfg) { Reconfigure(cfg); }

        public static string KeyFor(string path)
        {
            uint h = 2166136261;
            foreach (char c in path.TrimEnd('\\').ToLowerInvariant()) { h ^= c; h *= 16777619; }
            return "f_" + h.ToString("x8");
        }

        static TimeSpan ParseTime(string t)
        {
            TimeSpan ts;
            if (TimeSpan.TryParse(t ?? "", CultureInfo.InvariantCulture, out ts) && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1)) return ts;
            return TimeSpan.FromHours(3);
        }

        public static void Reconfigure(Config cfg)
        {
            lock (gate)
            {
                excludePrefixes = (cfg.SearchExclude ?? new string[0])
                    .Select(x => Environment.ExpandEnvironmentVariables(x ?? "").Trim())
                    .Where(x => x.Length > 2 && x.IndexOf('%') < 0)
                    .Select(x => x.TrimEnd('\\') + "\\")
                    .ToArray();
                excludeNames = new HashSet<string>(new[] { "$Recycle.Bin", "System Volume Information", "node_modules", ".git", ".svn", "__pycache__", "$WINDOWS.~BT", "$WinREAgent", "Config.Msi", "$SysReset", "#recycle", "@eaDir", ".snapshot", "~snapshot" }, StringComparer.OrdinalIgnoreCase);

                var old = Sources;
                var list = new List<IndexSource>();

                string[] localRoots = cfg.SearchRoots ?? new string[0];
                if (cfg.IndexLocalDrives || localRoots.Length > 0)
                {
                    list.Add(new IndexSource
                    {
                        Key = "local", Label = "내 PC 드라이브", CacheFile = "file_index.bin",
                        Watch = true, Mode = "interval", IntervalHours = 0.5,
                        RootsFn = () =>
                        {
                            if (localRoots.Length > 0) return localRoots.Select(r => Environment.ExpandEnvironmentVariables(r)).ToArray();
                            return IO.DriveInfo.GetDrives().Where(d => d.DriveType == IO.DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName).ToArray();
                        }
                    });
                }
                if (cfg.IncludeMappedDrives)
                {
                    list.Add(new IndexSource
                    {
                        Key = "mapped", Label = "네트워크 드라이브", CacheFile = "file_index_net.bin",
                        Watch = false, Mode = "interval", IntervalHours = 2,
                        RootsFn = () =>
                        {
                            try
                            {
                                return IO.DriveInfo.GetDrives().Where(d => d.DriveType == IO.DriveType.Network).Select(d => d.RootDirectory.FullName).ToArray();
                            }
                            catch (Exception) { return new string[0]; }
                        }
                    });
                }
                foreach (var f in cfg.Folders)
                {
                    string path = Environment.ExpandEnvironmentVariables(f.Path ?? "").Trim();
                    if (path.Length == 0) continue;
                    if (path.Length > 3) path = path.TrimEnd('\\');
                    string key = KeyFor(path);
                    if (list.Any(x => x.Key == key)) continue;
                    string root = path.Length == 2 && path[1] == ':' ? path + "\\" : path;
                    IndexSource src = null;
                    src = new IndexSource
                    {
                        Key = key, Label = path, CacheFile = "idx_" + key + ".bin",
                        Watch = f.Watch, Subfolders = f.Subfolders,
                        Mode = f.Rescan == "never" || f.Rescan == "interval" ? f.Rescan : "daily",
                        DailyTime = ParseTime(f.Time),
                        IntervalHours = Math.Max(1, f.Hours),
                        RootsFn = () =>
                        {
                            if (!Native.IsServerOnly(root)) { src.LastError = ""; return new[] { root }; }
                            // \\서버 만 적었으면 그 서버의 공유 폴더 전체를 색인
                            int err;
                            var shares = Native.ListShares(root, out err);
                            src.LastError = err != 0 ? Native.ShareError(err) : (shares.Count == 0 ? "공유 폴더가 없어요" : "");
                            return shares.Count > 0 ? shares.ToArray() : new[] { root };
                        }
                    };
                    list.Add(src);
                }

                // 같은 대상의 기존 색인은 그대로 넘겨받아서 다시 처음부터 훑지 않게
                foreach (var n in list)
                {
                    var o = old.FirstOrDefault(x => x.Key == n.Key);
                    if (o != null && !o.Scanning && o.Current.Complete) n.Current = o.Current;
                }
                foreach (var o in old) o.Stop();
                Sources = list.ToArray();
                foreach (var n in list) n.Start();
            }
        }

        public static IndexSource Find(string keyOrPath)
        {
            string key = keyOrPath == "local" || keyOrPath == "mapped" ? keyOrPath : KeyFor(Environment.ExpandEnvironmentVariables(keyOrPath ?? "").Trim());
            return Sources.FirstOrDefault(s => s.Key == key);
        }

        public static void RequestScan()
        {
            foreach (var s in Sources) s.RequestScan();
        }

        public static bool RescanPath(string keyOrPath)
        {
            var s = Find(keyOrPath);
            if (s == null) return false;
            s.RequestScan();
            return true;
        }

        public static bool AnyComplete
        {
            get { return Sources.Any(s => s.Current.Complete); }
        }

        public static string StatusText()
        {
            var src = Sources;
            if (src.Length == 0) return "색인할 대상이 없어요 · 검색 설정에서 폴더를 추가하세요";
            long total = src.Sum(s => (long)s.Current.Entries.Length);
            int scanning = src.Count(s => s.Scanning);
            int offline = src.Count(s => s.Offline);
            string t = "색인 " + total.ToString("N0") + "개 · " + src.Length + "곳";
            if (scanning > 0) t += " · 색인 중 " + scanning + "곳";
            if (offline > 0) t += " · 연결 안 됨 " + offline + "곳";
            return t;
        }

        public static bool IsExcluded(string path)
        {
            string p = path.TrimEnd('\\') + "\\";
            var ex = excludePrefixes;
            for (int i = 0; i < ex.Length; i++)
                if (p.StartsWith(ex[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool IsExcludedName(string name) { return excludeNames.Contains(name); }

        public static void AddOverlay(string path)
        {
            try
            {
                if (IsExcluded(path)) return;
                lock (overlayGate)
                {
                    overlay.Add(new KeyValuePair<DateTime, string>(DateTime.Now, path));
                    if (overlay.Count > 20000) overlay.RemoveRange(0, 5000);
                }
            }
            catch (Exception) { }
        }

        public static void TrimOverlay(DateTime before, string[] roots)
        {
            lock (overlayGate)
            {
                overlay.RemoveAll(kv => kv.Key < before && roots.Any(r => kv.Value.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)));
            }
        }

        static int Match(string dir, string name, bool isDir, string[] tokens, HashSet<string> exts, bool foldersOnly)
        {
            if (foldersOnly && !isDir) return -1;
            if (exts != null)
            {
                if (isDir) return -1;
                int dot = name.LastIndexOf('.');
                if (dot < 0 || !exts.Contains(name.Substring(dot + 1))) return -1;
            }
            int score = 2;
            for (int k = 0; k < tokens.Length; k++)
            {
                string t = tokens[k];
                if (t.IndexOf('\\') >= 0)
                {
                    if ((dir + "\\" + name).IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0) return -1;
                    continue;
                }
                int pos = name.IndexOf(t, StringComparison.OrdinalIgnoreCase);
                if (pos < 0) return -1;
                if (k == 0)
                {
                    if (pos == 0) score = 0;
                    else
                    {
                        char c = name[pos - 1];
                        if (c == '_' || c == '-' || c == ' ' || c == '.' || c == '(' || c == '[') score = 1;
                    }
                }
            }
            return score;
        }

        public static List<Hit> Search(string query, HashSet<string> exts, bool foldersOnly, int max, out int total)
        {
            return Search(query, exts, foldersOnly, null, max, out total);
        }

        static string Slash(string p) { return p.TrimEnd('\\') + "\\"; }

        static bool InAny(string dir, string[] prefixes)
        {
            string d = Slash(dir);
            for (int i = 0; i < prefixes.Length; i++)
                if (d.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // 위치(디스크·서버) 목록: 검색 창의 위치 칩에 씀
        public static List<KeyValuePair<string, string[]>> Locations()
        {
            var list = new List<KeyValuePair<string, string[]>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var src in Sources)
            {
                if (src.Key == "local" || src.Key == "mapped")
                {
                    foreach (var r in src.Roots)
                    {
                        string label = r.TrimEnd('\\');
                        if (seen.Add(label)) list.Add(new KeyValuePair<string, string[]>(label, new[] { Slash(r) }));
                    }
                }
                else
                {
                    string label = src.Label.TrimEnd('\\');
                    int cut = label.LastIndexOf('\\');
                    string shortName = Native.IsServerOnly(label) ? label.Substring(2) : (cut >= 0 ? label.Substring(cut + 1) : label);
                    if (shortName.Length == 0) shortName = label;
                    var roots = src.Roots.Length > 0 ? src.Roots : new[] { label };
                    if (seen.Add(shortName)) list.Add(new KeyValuePair<string, string[]>(shortName, roots.Select(Slash).ToArray()));
                }
            }
            return list;
        }

        public static List<Hit> Search(string query, HashSet<string> exts, bool foldersOnly, string[] places, int max, out int total)
        {
            total = 0;
            var result = new List<Hit>();
            string[] tokens = (query ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 && exts == null && !foldersOnly) return result;
            var buckets = new[] { new List<Hit>(), new List<Hit>(), new List<Hit>() };

            // 최근에 생긴 파일 (색인 이후)
            List<KeyValuePair<DateTime, string>> recent;
            lock (overlayGate) recent = new List<KeyValuePair<DateTime, string>>(overlay);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = recent.Count - 1; i >= 0 && i >= recent.Count - 3000; i--)
            {
                string p = recent[i].Value;
                if (!seen.Add(p)) continue;
                string name = IO.Path.GetFileName(p);
                string dir = IO.Path.GetDirectoryName(p) ?? "";
                bool quick = true;
                foreach (var t in tokens)
                {
                    string target = t.IndexOf('\\') >= 0 ? p : name;
                    if (target.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0) { quick = false; break; }
                }
                if (!quick) continue;
                if (places != null && !InAny(dir, places)) continue;
                bool isDir = IO.Directory.Exists(p);
                if (!isDir && !IO.File.Exists(p)) continue;
                int sc = Match(dir, name, isDir, tokens, exts, foldersOnly);
                if (sc < 0) continue;
                total++;
                if (buckets[sc].Count < max) buckets[sc].Add(new Hit { Dir = dir.TrimEnd('\\'), Name = name, IsDir = isDir, Score = sc, Remote = p.StartsWith("\\\\") });
            }

            foreach (var src in Sources)
            {
                if (places != null)
                {
                    // 고른 위치와 전혀 겹치지 않는 색인은 통째로 건너뜀
                    bool related = src.Roots.Length == 0 || src.Roots.Any(r => places.Any(pl => Slash(r).StartsWith(pl, StringComparison.OrdinalIgnoreCase) || pl.StartsWith(Slash(r), StringComparison.OrdinalIgnoreCase)));
                    if (!related) continue;
                }
                var snap = src.Current;
                var entries = snap.Entries;
                var dirs = snap.Dirs;
                bool remote = src.Key != "local";
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    int sc = Match(dirs[e.Dir], e.Name, e.IsDir, tokens, exts, foldersOnly);
                    if (sc < 0) continue;
                    if (places != null && !InAny(dirs[e.Dir], places)) continue;
                    if (seen.Count > 0 && seen.Contains(dirs[e.Dir] + "\\" + e.Name)) continue;
                    total++;
                    if (buckets[sc].Count < max) buckets[sc].Add(new Hit { Dir = dirs[e.Dir], Name = e.Name, IsDir = e.IsDir, Score = sc, Remote = remote });
                }
            }

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in buckets)
            {
                b.Sort((x, y) =>
                {
                    int c = x.Name.Length.CompareTo(y.Name.Length);
                    return c != 0 ? c : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                });
                foreach (var h in b)
                {
                    if (result.Count >= max) break;
                    if (unique.Add(h.FullPath)) result.Add(h);
                }
            }
            return result;
        }

        // 화면에 보일 결과만 크기/날짜를 읽고, 이미 지워진 파일은 뺍니다.
        public static List<Hit> FillDetails(List<Hit> hits)
        {
            var ok = new List<Hit>(hits.Count);
            var offlineRoots = Sources.Where(s => s.Offline).SelectMany(s => s.Roots).ToArray();
            int checkedCount = 0;
            foreach (var h in hits)
            {
                bool off = h.Remote && offlineRoots.Any(r => h.Dir.StartsWith(r.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                // 서버가 끊겼거나 결과가 많으면 앞쪽만 확인 (느려지지 않게)
                if (off || checkedCount >= 120) { ok.Add(h); continue; }
                checkedCount++;
                try
                {
                    if (h.IsDir)
                    {
                        var di = new IO.DirectoryInfo(h.FullPath);
                        if (!di.Exists) continue;
                        h.Modified = di.LastWriteTime;
                    }
                    else
                    {
                        var fi = new IO.FileInfo(h.FullPath);
                        if (!fi.Exists) continue;
                        h.Size = fi.Length;
                        h.Modified = fi.LastWriteTime;
                    }
                    ok.Add(h);
                }
                catch (Exception)
                {
                    ok.Add(h);
                }
            }
            return ok;
        }
    }

    class FileSearchWindow : Window
    {
        static readonly string[] CHIP_NAMES = { "전체", "CAD·3D", "PDF", "문서", "이미지", "폴더" };
        static readonly string[][] CHIP_EXTS = {
            null,
            new[] { "dwg", "dxf", "rvt", "rfa", "skp", "3dm", "ifc", "max", "dgn" },
            new[] { "pdf" },
            new[] { "hwp", "hwpx", "doc", "docx", "xls", "xlsx", "xlsm", "ppt", "pptx", "txt", "csv" },
            new[] { "jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "webp", "heic", "psd" },
            null
        };
        const int FOLDER_CHIP = 5;
        const int MAX = 300;

        static Color INK { get { return Theme.Fg; } }
        static Color MUTED { get { return Theme.Muted; } }
        static Color SOFT { get { return Theme.Sel; } }
        static Color HOVER { get { return Theme.Sel; } }
        static Color LINE { get { return Theme.Line; } }
        static Color PAPER { get { return Theme.Field; } }

        static string lastQuery = "";
        static int lastFilter = 0;
        static string lastPlace = "";

        List<KeyValuePair<string, string[]>> places = new List<KeyValuePair<string, string[]>>();
        List<Border> placeChips = new List<Border>();
        HashSet<string> placeSel = new HashSet<string>(); // 비어 있으면 전체 위치 (여러 개 고를 수 있음)

        RingApp app;
        Color accent;
        Color selBg;
        TextBox box;
        TextBlock placeholder;
        Border[] chips;
        int filter;
        StackPanel list;
        ScrollViewer scroller;
        TextBlock status;
        TextBlock countText;
        List<Hit> hits = new List<Hit>();
        List<Border> rows = new List<Border>();
        int sel = -1;
        int version;
        int lastTotal = -1;
        DispatcherTimer debounce;
        DispatcherTimer statusTimer;
        bool closing;

        public static void Open(RingApp app)
        {
            var w = new FileSearchWindow(app);
            w.Show();
            w.Activate();
            Native.SetForegroundWindow(new WindowInteropHelper(w).Handle);
            w.box.Focus();
            Keyboard.Focus(w.box);
            w.box.SelectAll();
        }

        FileSearchWindow(RingApp owner)
        {
            app = owner;
            try { accent = (Color)ColorConverter.ConvertFromString(app.Cfg.Accent); }
            catch (Exception) { accent = Color.FromRgb(0xD9, 0x77, 0x57); }
            selBg = Color.FromArgb(0x2E, accent.R, accent.G, accent.B);

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Title = "파일 검색";

            var card = new Border();
            card.Width = 780;
            card.Background = Theme.B(Theme.Bg);
            card.BorderBrush = Theme.B(Theme.Line);
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(20);
            card.Padding = new Thickness(20, 20, 20, 16);
            card.Margin = new Thickness(18);
            card.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = 0.28, Direction = 270 };
            var stack = new StackPanel();
            card.Child = stack;
            card.LayoutTransform = new ScaleTransform(app.UiScale, app.UiScale);
            Content = card;

            stack.Children.Add(Header());
            stack.Children.Add(PlaceRow());
            stack.Children.Add(SearchBox());
            stack.Children.Add(ChipRow());
            stack.Children.Add(ColumnHeader());

            list = new StackPanel();
            scroller = new ScrollViewer();
            scroller.Height = 360;
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.Content = list;
            stack.Children.Add(scroller);
            stack.Children.Add(Footer());

            debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            debounce.Tick += (s, e) => { debounce.Stop(); RunSearch(); };
            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            statusTimer.Tick += (s, e) => UpdateStatus();
            statusTimer.Start();

            filter = app.Cfg.SearchKeepQuery ? lastFilter : Math.Max(0, Math.Min(CHIP_NAMES.Length - 1, app.Cfg.SearchDefaultFilter));
            PaintChips();
            box.Text = app.Cfg.SearchKeepQuery ? lastQuery : "";
            if (app.Cfg.SearchKeepQuery) foreach (var k in lastPlace.Split('|')) if (k.Length > 0) placeSel.Add(k);
            PaintPlaces();
            if (lastQuery.Length == 0 && filter == 0) ShowEmpty();

            Loaded += (s, e) =>
            {
                var tl = app.PlaceInMonitor(app.LastPoint, ActualWidth, ActualHeight);
                Left = tl.X; Top = tl.Y;
            };
            Deactivated += (s, e) => SafeClose();
            Closed += (s, e) => { statusTimer.Stop(); debounce.Stop(); };
            PreviewKeyDown += OnKey;
            UpdateStatus();
        }

        void SafeClose()
        {
            if (closing) return;
            closing = true;
            lastQuery = box.Text;
            lastFilter = filter;
            lastPlace = string.Join("|", placeSel.ToArray());
            Close();
        }

        void PlaceNear(Point p)
        {
            var tl = app.PlaceInMonitor(p, ActualWidth, ActualHeight);
            Left = tl.X; Top = tl.Y;
        }

        // ----- 작은 부품
        TextBlock Text(string t, double size, Color c, bool bold)
        {
            var tb = new TextBlock();
            tb.Text = t;
            tb.FontFamily = Theme.Font;
            tb.FontSize = size;
            tb.Foreground = new SolidColorBrush(c);
            if (bold) tb.FontWeight = FontWeights.Bold;
            return tb;
        }

        TextBlock Glyph(string g, double size, Color c)
        {
            var tb = new TextBlock();
            tb.Text = g;
            tb.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            tb.FontSize = size;
            tb.Foreground = new SolidColorBrush(c);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            return tb;
        }

        UIElement Header()
        {
            var row = new DockPanel();
            row.Margin = new Thickness(0, 0, 0, 16);
            var badge = new Border();
            badge.Width = badge.Height = 34;
            badge.CornerRadius = new CornerRadius(17);
            badge.Background = new SolidColorBrush(accent);
            badge.Child = Glyph("", 15, Colors.White);
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge);

            var close = new Border();
            close.Width = close.Height = 34;
            close.CornerRadius = new CornerRadius(8);
            close.Background = Brushes.Transparent;
            close.Cursor = Cursors.Hand;
            close.ToolTip = "닫기 (Esc)";
            close.Child = Glyph("", 12, MUTED);
            close.MouseEnter += (s, e) => close.Background = new SolidColorBrush(SOFT);
            close.MouseLeave += (s, e) => close.Background = Brushes.Transparent;
            close.MouseLeftButtonDown += (s, e) => { e.Handled = true; SafeClose(); };
            DockPanel.SetDock(close, Dock.Right);
            row.Children.Add(close);

            var gear = new Border();
            gear.Width = gear.Height = 34;
            gear.CornerRadius = new CornerRadius(8);
            gear.Background = Brushes.Transparent;
            gear.Cursor = Cursors.Hand;
            gear.ToolTip = "검색 설정";
            gear.Margin = new Thickness(0, 0, 2, 0);
            gear.Child = Glyph("\uE713", 14, MUTED);
            gear.MouseEnter += (s, e) => gear.Background = new SolidColorBrush(SOFT);
            gear.MouseLeave += (s, e) => gear.Background = Brushes.Transparent;
            gear.MouseLeftButtonDown += (s, e) => { e.Handled = true; SafeClose(); app.OpenSearchSettings(); };
            DockPanel.SetDock(gear, Dock.Right);
            row.Children.Add(gear);

            var titles = new StackPanel();
            titles.Margin = new Thickness(10, 0, 0, 0);
            titles.VerticalAlignment = VerticalAlignment.Center;
            titles.Children.Add(Text("파일 검색", 15, INK, true));
            titles.Children.Add(Text("PC 전체에서 파일 이름으로 바로 찾아요", 11.5, MUTED, false));
            row.Children.Add(titles);
            return row;
        }

        UIElement SearchBox()
        {
            var border = new Border();
            border.CornerRadius = new CornerRadius(10);
            border.BorderBrush = new SolidColorBrush(accent);
            border.BorderThickness = new Thickness(1.5);
            border.Background = new SolidColorBrush(PAPER);
            var grid = new Grid();
            var icon = Glyph("", 14, MUTED);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.Margin = new Thickness(14, 0, 0, 0);
            box = new TextBox();
            box.BorderThickness = new Thickness(0);
            box.Background = Brushes.Transparent;
            box.FontFamily = Theme.Font;
            box.FontSize = 17;
            box.Padding = new Thickness(36, 9, 12, 9);
            box.Foreground = new SolidColorBrush(INK);
            placeholder = Text("예: 배치도   ·   구조 검토   ·   2026\\평면도", 15, Color.FromRgb(0x9A, 0x98, 0x90), false);
            placeholder.Margin = new Thickness(40, 11, 12, 10);
            placeholder.IsHitTestVisible = false;
            box.TextChanged += (s, e) =>
            {
                placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                debounce.Stop();
                debounce.Start();
            };
            grid.Children.Add(box);
            grid.Children.Add(icon);
            grid.Children.Add(placeholder);
            border.Child = grid;
            return border;
        }

        UIElement ChipRow()
        {
            var row = new WrapPanel();
            row.Margin = new Thickness(0, 12, 0, 12);
            chips = new Border[CHIP_NAMES.Length];
            for (int i = 0; i < CHIP_NAMES.Length; i++)
            {
                int idx = i;
                var chip = new Border();
                chip.CornerRadius = new CornerRadius(15);
                chip.Padding = new Thickness(12, 5, 12, 6);
                chip.Margin = new Thickness(0, 0, 6, 0);
                chip.Cursor = Cursors.Hand;
                chip.Child = Text(CHIP_NAMES[i], 12.5, INK, false);
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    filter = idx;
                    PaintChips();
                    RunSearch();
                    box.Focus();
                };
                chips[i] = chip;
                row.Children.Add(chip);
            }
            return row;
        }

        // 검색창 위 오른쪽: 디스크·서버별 보기
        UIElement PlaceRow()
        {
            var right = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 10) };
            var pin = Glyph("\uE707", 12, MUTED);
            pin.Margin = new Thickness(0, 0, 8, 0);
            pin.ToolTip = "위치";
            right.Children.Add(pin);
            places = FileIndex.Locations();
            var all = new List<KeyValuePair<string, string[]>> { new KeyValuePair<string, string[]>("", null) };
            all.AddRange(places);
            foreach (var pl in all)
            {
                string key = pl.Key;
                var chip = new Border();
                chip.CornerRadius = new CornerRadius(15);
                chip.Padding = new Thickness(12, 5, 12, 6);
                chip.Cursor = Cursors.Hand;
                chip.Tag = key;
                chip.BorderThickness = new Thickness(1);
                chip.Child = Text(key.Length == 0 ? "전체 위치" : key, 12.5, INK, false);
                chip.ToolTip = pl.Value == null ? "모든 디스크와 서버" : string.Join("\n", pl.Value) + "\n(여러 개를 함께 고를 수 있어요)";
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    if (key.Length == 0) placeSel.Clear();          // 전체 위치
                    else if (!placeSel.Remove(key)) placeSel.Add(key); // 눌렀다 다시 누르면 빠짐
                    PaintPlaces();
                    RunSearch();
                    box.Focus();
                };
                chip.Margin = new Thickness(placeChips.Count == 0 ? 0 : 6, 0, 0, 0);
                placeChips.Add(chip);
                right.Children.Add(chip);
            }
            return right;
        }

        void PaintPlaces()
        {
            placeSel.RemoveWhere(k => !places.Any(p => p.Key == k));
            foreach (var c in placeChips)
            {
                string tag = (string)c.Tag;
                bool on = tag.Length == 0 ? placeSel.Count == 0 : placeSel.Contains(tag);
                c.Background = on ? new SolidColorBrush(INK) : Brushes.Transparent;
                c.BorderBrush = new SolidColorBrush(on ? INK : LINE);
                var tb = (TextBlock)c.Child;
                tb.Foreground = on ? Theme.B(Theme.Bg) : new SolidColorBrush(INK);
                tb.FontWeight = on ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        string[] PlacePrefixes()
        {
            if (placeSel.Count == 0) return null;
            var list = new List<string>();
            foreach (var p in places) if (placeSel.Contains(p.Key)) list.AddRange(p.Value);
            return list.Count > 0 ? list.ToArray() : null;
        }

        void PaintChips()
        {
            for (int i = 0; i < chips.Length; i++)
            {
                bool on = i == filter;
                chips[i].Background = new SolidColorBrush(on ? accent : SOFT);
                var tb = (TextBlock)chips[i].Child;
                tb.Foreground = new SolidColorBrush(on ? Colors.White : INK);
                tb.FontWeight = on ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        Grid Columns()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
            return g;
        }

        UIElement ColumnHeader()
        {
            var b = new Border();
            b.Padding = new Thickness(10, 4, 18, 6);
            b.BorderBrush = new SolidColorBrush(SOFT);
            b.BorderThickness = new Thickness(0, 0, 0, 1);
            var g = Columns();
            string[] names = { "이름", "폴더", "수정한 날짜", "크기" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = Text(names[i], 11.5, MUTED, false);
                if (i == 3) t.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(t, i);
                g.Children.Add(t);
            }
            b.Child = g;
            return b;
        }

        UIElement Footer()
        {
            var row = new DockPanel();
            row.Margin = new Thickness(2, 12, 0, 0);
            var hint = Text("↑↓ 이동 · Enter 열기 · Ctrl+Enter 폴더 열기 · Ctrl+C 경로 복사 · Esc 닫기", 11.5, MUTED, false);
            DockPanel.SetDock(hint, Dock.Right);
            row.Children.Add(hint);
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            countText = Text("", 11.5, INK, true);
            countText.Margin = new Thickness(0, 0, 8, 0);
            status = Text("", 11.5, MUTED, false);
            var reindex = Text("다시 색인", 11.5, accent, false);
            reindex.Margin = new Thickness(8, 0, 0, 0);
            reindex.Cursor = Cursors.Hand;
            reindex.TextDecorations = TextDecorations.Underline;
            reindex.ToolTip = "드라이브 전체를 다시 훑어요 (백그라운드)";
            reindex.MouseLeftButtonUp += (s, e) => { FileIndex.RequestScan(); UpdateStatus(); };
            var settings = Text("검색 설정", 11.5, accent, false);
            settings.Margin = new Thickness(10, 0, 0, 0);
            settings.Cursor = Cursors.Hand;
            settings.TextDecorations = TextDecorations.Underline;
            settings.MouseLeftButtonUp += (s, e) => { SafeClose(); app.OpenSearchSettings(); };
            left.Children.Add(countText);
            left.Children.Add(status);
            row.Children.Add(left);
            return row;
        }

        // ----- 상태 표시
        void UpdateStatus()
        {
            status.Text = FileIndex.StatusText();
            countText.Text = lastTotal >= 0 ? lastTotal.ToString("N0") + "개 찾음 ·" : "";
        }

        // ----- 검색
        void RunSearch()
        {
            string q = box.Text.Trim();
            int f = filter;
            HashSet<string> exts = CHIP_EXTS[f] == null ? null : new HashSet<string>(CHIP_EXTS[f], StringComparer.OrdinalIgnoreCase);
            bool folders = f == FOLDER_CHIP;
            string[] pl = PlacePrefixes();
            if (q.Length == 0 && f == 0) { lastTotal = -1; ShowEmpty(); UpdateStatus(); return; }
            int v = ++version;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int total;
                List<Hit> r;
                try
                {
                    r = FileIndex.Search(q, exts, folders, pl, MAX, out total);
                    r = FileIndex.FillDetails(r);
                }
                catch (Exception) { r = new List<Hit>(); total = 0; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (v != version || closing) return;
                    lastTotal = total;
                    Render(r, q);
                    UpdateStatus();
                }));
            });
        }

        void ShowEmpty()
        {
            list.Children.Clear();
            rows.Clear();
            hits = new List<Hit>();
            sel = -1;
            var t = Text("파일 이름의 일부를 입력하세요", 14, INK, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 110, 0, 8);
            var t2 = Text("띄어 쓰면 모든 단어가 들어간 파일을 찾아요 · 폴더 이름으로 좁히려면 \\ 를 넣으세요 (예: 2026\\배치도)", 12, MUTED, false);
            t2.HorizontalAlignment = HorizontalAlignment.Center;
            list.Children.Add(t);
            list.Children.Add(t2);
        }

        static string FormatSize(long b)
        {
            if (b < 0) return "";
            if (b < 1024) return "1 KB";
            double kb = b / 1024.0;
            if (kb < 1024) return Math.Ceiling(kb).ToString("N0") + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.0") + " MB";
            return (mb / 1024.0).ToString("0.0") + " GB";
        }

        static void TagFor(Hit h, out string label, out Color bg, out Color fg)
        {
            if (h.IsDir) { label = "폴더"; bg = Color.FromRgb(0xF6, 0xEE, 0xDC); fg = Color.FromRgb(0x8A, 0x64, 0x14); return; }
            int dot = h.Name.LastIndexOf('.');
            string ext = dot >= 0 ? h.Name.Substring(dot + 1).ToLowerInvariant() : "";
            label = ext.Length == 0 ? "파일" : (ext.Length > 4 ? ext.Substring(0, 4) : ext).ToUpperInvariant();
            if (CHIP_EXTS[1].Contains(ext)) { bg = Color.FromRgb(0xE6, 0xEE, 0xFB); fg = Color.FromRgb(0x2C, 0x5B, 0xB5); }
            else if (ext == "pdf") { bg = Color.FromRgb(0xFB, 0xE7, 0xE4); fg = Color.FromRgb(0xB2, 0x3A, 0x2C); }
            else if (CHIP_EXTS[4].Contains(ext)) { bg = Color.FromRgb(0xE5, 0xF3, 0xEA); fg = Color.FromRgb(0x2B, 0x7A, 0x4B); }
            else if (CHIP_EXTS[3].Contains(ext)) { bg = Color.FromRgb(0xE7, 0xF1, 0xFA); fg = Color.FromRgb(0x1F, 0x6A, 0xA5); }
            else { bg = Color.FromRgb(0xEE, 0xED, 0xE8); fg = Color.FromRgb(0x5E, 0x5D, 0x59); }
        }

        TextBlock HighlightedName(string name, string query)
        {
            var tb = new TextBlock();
            tb.FontFamily = Theme.Font;
            tb.FontSize = 13.5;
            tb.Foreground = new SolidColorBrush(INK);
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            tb.VerticalAlignment = VerticalAlignment.Center;
            string token = (query ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(t => t.IndexOf('\\') < 0);
            int pos = string.IsNullOrEmpty(token) ? -1 : name.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (pos < 0)
            {
                tb.Text = name;
                return tb;
            }
            if (pos > 0) tb.Inlines.Add(new Run(name.Substring(0, pos)));
            var hit = new Run(name.Substring(pos, token.Length));
            hit.FontWeight = FontWeights.Bold;
            hit.Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0x5C, 0x3E));
            tb.Inlines.Add(hit);
            if (pos + token.Length < name.Length) tb.Inlines.Add(new Run(name.Substring(pos + token.Length)));
            return tb;
        }

        void Render(List<Hit> r, string query)
        {
            list.Children.Clear();
            rows.Clear();
            hits = r;
            sel = -1;
            if (r.Count == 0)
            {
                var t = Text("찾는 파일이 없어요", 14, INK, true);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 110, 0, 8);
                var t2 = Text(FileIndex.AnyComplete ? "철자를 바꾸거나 종류 필터를 '전체'로 바꿔 보세요" : "아직 색인 중이에요. 잠시 뒤 다시 찾아보세요", 12, MUTED, false);
                t2.HorizontalAlignment = HorizontalAlignment.Center;
                list.Children.Add(t);
                list.Children.Add(t2);
                return;
            }
            for (int i = 0; i < r.Count; i++)
            {
                int idx = i;
                var h = r[i];
                var row = new Border();
                row.CornerRadius = new CornerRadius(8);
                row.Padding = new Thickness(10, 6, 10, 6);
                row.Background = Brushes.Transparent;
                row.Cursor = Cursors.Hand;
                row.ToolTip = h.FullPath;
                var g = Columns();

                var nameCell = new DockPanel();
                string label; Color bg, fg;
                TagFor(h, out label, out bg, out fg);
                var tag = new Border();
                tag.Width = 40;
                tag.CornerRadius = new CornerRadius(4);
                tag.Background = new SolidColorBrush(bg);
                tag.Margin = new Thickness(0, 0, 10, 0);
                tag.Padding = new Thickness(0, 2, 0, 2);
                var tagText = Text(label, 9.5, fg, true);
                tagText.HorizontalAlignment = HorizontalAlignment.Center;
                tag.Child = tagText;
                DockPanel.SetDock(tag, Dock.Left);
                nameCell.Children.Add(tag);
                nameCell.Children.Add(HighlightedName(h.Name, query));
                Grid.SetColumn(nameCell, 0);
                g.Children.Add(nameCell);

                var folder = Text(h.Dir, 12, MUTED, false);
                folder.TextTrimming = TextTrimming.CharacterEllipsis;
                folder.VerticalAlignment = VerticalAlignment.Center;
                folder.Margin = new Thickness(10, 0, 8, 0);
                Grid.SetColumn(folder, 1);
                g.Children.Add(folder);

                var date = Text(h.Modified == DateTime.MinValue ? "" : h.Modified.ToString("yyyy-MM-dd HH:mm"), 12, MUTED, false);
                date.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(date, 2);
                g.Children.Add(date);

                var size = Text(h.IsDir ? "" : FormatSize(h.Size), 12, MUTED, false);
                size.HorizontalAlignment = HorizontalAlignment.Right;
                size.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(size, 3);
                g.Children.Add(size);

                row.Child = g;
                row.MouseEnter += (s, e) => { if (sel != idx) row.Background = new SolidColorBrush(HOVER); };
                row.MouseLeave += (s, e) => { if (sel != idx) row.Background = Brushes.Transparent; };
                row.MouseLeftButtonDown += (s, e) =>
                {
                    Select(idx);
                    if (e.ClickCount == 2) OpenSelected(false);
                };
                row.ContextMenu = RowMenu(idx);
                rows.Add(row);
                list.Children.Add(row);
            }
            scroller.ScrollToTop();
            Select(0);
        }

        ContextMenu RowMenu(int idx)
        {
            var m = new ContextMenu();
            var a = new MenuItem { Header = "열기" };
            a.Click += (s, e) => { Select(idx); OpenSelected(false); };
            var b = new MenuItem { Header = "폴더 열기" };
            b.Click += (s, e) => { Select(idx); OpenSelected(true); };
            var c = new MenuItem { Header = "경로 복사" };
            c.Click += (s, e) => { Select(idx); CopySelected(); };
            m.Items.Add(a); m.Items.Add(b); m.Items.Add(c);
            return m;
        }

        void Select(int i)
        {
            if (rows.Count == 0) { sel = -1; return; }
            i = Math.Max(0, Math.Min(rows.Count - 1, i));
            if (sel >= 0 && sel < rows.Count) rows[sel].Background = Brushes.Transparent;
            sel = i;
            rows[sel].Background = new SolidColorBrush(selBg);
            rows[sel].BringIntoView();
        }

        void OpenSelected(bool folder)
        {
            if (sel < 0 || sel >= hits.Count) return;
            var h = hits[sel];
            try
            {
                if (folder) Process.Start("explorer.exe", "/select,\"" + h.FullPath + "\"");
                else Process.Start(h.FullPath);
                SafeClose();
            }
            catch (Exception ex)
            {
                app.Toast("열지 못했어요: " + ex.Message, 3500);
            }
        }

        void CopySelected()
        {
            if (sel < 0 || sel >= hits.Count) return;
            try
            {
                Clipboard.SetText(hits[sel].FullPath);
                app.Toast("경로를 복사했어요", 1600);
            }
            catch (Exception)
            {
                app.Toast("클립보드를 쓰지 못했어요. 다시 시도해 주세요", 2000);
            }
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (e.Key == Key.Escape) { e.Handled = true; SafeClose(); }
            else if (e.Key == Key.Down) { e.Handled = true; Select(sel + 1); }
            else if (e.Key == Key.Up) { e.Handled = true; Select(sel - 1); }
            else if (e.Key == Key.PageDown) { e.Handled = true; Select(sel + 10); }
            else if (e.Key == Key.PageUp) { e.Handled = true; Select(sel - 10); }
            else if (e.Key == Key.Enter) { e.Handled = true; OpenSelected(ctrl); }
            else if (ctrl && e.Key == Key.C && box.SelectionLength == 0 && sel >= 0) { e.Handled = true; CopySelected(); }
        }
    }

    // ------------------------------------------------------------ 검색 설정 창 (Everything 옵션 > 폴더 와 비슷하게)
    class SearchSettingsPanel : StackPanel
    {

        RingApp app;
        Config cfg;
        List<FolderSetting> folders;
        ListBox folderList;
        List<TextBlock> statusBlocks = new List<TextBlock>();
        CheckBox chkLocal, chkMapped, chkSub, chkWatch;
        TextBlock lblLocal, lblMapped;
        RadioButton rDaily, rInterval, rNever;
        ComboBox cmbTime;
        TextBox txtHours, txtPath, txtExclude;
        GroupBox grpFolder;
        Panel folderPanel;
        bool loading;
        DispatcherTimer refresh;
        DispatcherTimer applyTimer;
        public bool AutoApply;

        void Changed()
        {
            if (!AutoApply) return;
            applyTimer.Stop();
            applyTimer.Start();
        }

        public SearchSettingsPanel(RingApp owner, bool standalone)
        {
            app = owner;
            cfg = Config.Load();
            folders = cfg.Folders.Select(f => f.Clone()).ToList();

            var root = this;
            applyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            applyTimer.Tick += (s, e) => { applyTimer.Stop(); Apply(false); };

            root.Children.Add(DrivesGroup());
            root.Children.Add(FoldersGroup());
            root.Children.Add(FolderSettingsGroup());
            root.Children.Add(ExcludeGroup());
            if (standalone) root.Children.Add(Buttons());

            RebuildList(folders.Count > 0 ? 0 : -1);

            refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            refresh.Tick += (s, e) => RefreshStatus();
            Loaded += (s, e) => refresh.Start();
            Unloaded += (s, e) => refresh.Stop();
            RefreshStatus();
        }

        // ----- 부품
        static TextBlock Small(string t)
        {
            var tb = new TextBlock();
            tb.Text = t;
            tb.FontSize = 12;
            tb.Foreground = Theme.B(Theme.Muted);
            tb.TextWrapping = TextWrapping.Wrap;
            return tb;
        }

        static Button Btn(string t, double width)
        {
            var b = new Button();
            b.Content = t;
            b.MinWidth = width;
            b.Padding = new Thickness(12, 5, 12, 5);
            b.Margin = new Thickness(0, 0, 0, 8);
            return b;
        }

        static GroupBox Group(string header)
        {
            var g = new GroupBox();
            g.Header = header;
            g.Padding = new Thickness(10);
            g.Margin = new Thickness(0, 0, 0, 12);
            return g;
        }

        // ----- 드라이브
        UIElement DrivesGroup()
        {
            var g = Group("드라이브");
            var sp = new StackPanel();
            chkLocal = new CheckBox { Content = "이 PC의 로컬 드라이브 모두 색인 (C:, D: …)", IsChecked = cfg.IndexLocalDrives };
            lblLocal = Small("");
            lblLocal.Margin = new Thickness(22, 2, 0, 8);
            chkMapped = new CheckBox { Content = "네트워크 드라이브(Z: 등 드라이브 문자로 연결된 서버) 자동 포함", IsChecked = cfg.IncludeMappedDrives };
            lblMapped = Small("");
            lblMapped.Margin = new Thickness(22, 2, 0, 0);
            chkLocal.Click += (s, e) => Changed();
            chkMapped.Click += (s, e) => Changed();
            sp.Children.Add(chkLocal);
            sp.Children.Add(lblLocal);
            sp.Children.Add(chkMapped);
            sp.Children.Add(lblMapped);
            g.Content = sp;
            return g;
        }

        // ----- 폴더 목록
        UIElement FoldersGroup()
        {
            var g = Group("폴더 (회사 서버 등)");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            folderList = new ListBox();
            folderList.Height = 150;
            folderList.Margin = new Thickness(0, 0, 12, 0);
            folderList.SelectionChanged += (s, e) => LoadSelected();
            Grid.SetColumn(folderList, 0);
            grid.Children.Add(folderList);

            var btns = new StackPanel();
            Grid.SetColumn(btns, 1);
            var add = Btn("추가…", 130);
            add.Click += (s, e) => BrowseAdd();
            var remove = Btn("제거", 130);
            remove.Click += (s, e) => RemoveSelected();
            var rescan = Btn("지금 재스캔", 130);
            rescan.Click += (s, e) => RescanSelected();
            var rescanAll = Btn("모두 재스캔", 130);
            rescanAll.Click += (s, e) => { Apply(false); FileIndex.RequestScan(); app.Toast("모든 색인을 다시 훑는 중이에요", 2000); };
            btns.Children.Add(add);
            btns.Children.Add(remove);
            btns.Children.Add(rescan);
            btns.Children.Add(rescanAll);
            grid.Children.Add(btns);

            var row = new DockPanel();
            row.Margin = new Thickness(0, 8, 12, 0);
            Grid.SetRow(row, 1);
            Grid.SetColumnSpan(row, 1);
            var addPath = new Button { Content = "경로 추가", Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(8, 0, 0, 0) };
            addPath.Click += (s, e) => { AddFolder(txtPath.Text); txtPath.Text = ""; };
            DockPanel.SetDock(addPath, Dock.Right);
            row.Children.Add(addPath);
            txtPath = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
            txtPath.ToolTip = @"예: \\10.208.128.52\디자인2팀   또는   D:\프로젝트";
            txtPath.KeyDown += (s, e) => { if (e.Key == Key.Enter) { AddFolder(txtPath.Text); txtPath.Text = ""; } };
            row.Children.Add(txtPath);
            grid.Children.Add(row);

            var tip = Small(@"서버 주소를 직접 넣을 수 있어요. 예: \\10.208.128.52\디자인2팀");
            tip.Margin = new Thickness(0, 4, 0, 0);
            var sp = new StackPanel();
            sp.Children.Add(grid);
            sp.Children.Add(tip);
            g.Content = sp;
            return g;
        }

        // ----- 선택한 폴더 설정
        UIElement FolderSettingsGroup()
        {
            grpFolder = Group("폴더 설정");
            var sp = new StackPanel();
            folderPanel = sp;

            chkSub = new CheckBox { Content = "하위 폴더 색인", Margin = new Thickness(0, 0, 0, 6) };
            chkWatch = new CheckBox { Content = "변경 사항 감시 시도 (새 파일을 바로 검색에 반영)", Margin = new Thickness(0, 0, 0, 10) };
            chkSub.Checked += (s, e) => Edit(f => f.Subfolders = true);
            chkSub.Unchecked += (s, e) => Edit(f => f.Subfolders = false);
            chkWatch.Checked += (s, e) => Edit(f => f.Watch = true);
            chkWatch.Unchecked += (s, e) => Edit(f => f.Watch = false);
            sp.Children.Add(chkSub);
            sp.Children.Add(chkWatch);

            // 매일
            var r1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            rDaily = new RadioButton { Content = "재스캔: 매일", GroupName = "rescan", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };
            cmbTime = new ComboBox { Width = 90, Margin = new Thickness(8, 0, 0, 0) };
            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += 30)
                    cmbTime.Items.Add(h.ToString("00") + ":" + m.ToString("00"));
            r1.Children.Add(rDaily);
            r1.Children.Add(cmbTime);
            sp.Children.Add(r1);

            // 주기적
            var r2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            rInterval = new RadioButton { Content = "주기적 재스캔:", GroupName = "rescan", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };
            txtHours = new TextBox { Width = 50, Margin = new Thickness(8, 0, 6, 0), Padding = new Thickness(3, 2, 3, 2), HorizontalContentAlignment = HorizontalAlignment.Right };
            var hoursLabel = new TextBlock { Text = "시간마다", VerticalAlignment = VerticalAlignment.Center };
            r2.Children.Add(rInterval);
            r2.Children.Add(txtHours);
            r2.Children.Add(hoursLabel);
            sp.Children.Add(r2);

            rNever = new RadioButton { Content = "재스캔 안 함 (\"지금 재스캔\"을 누를 때만)", GroupName = "rescan" };
            sp.Children.Add(rNever);

            rDaily.Checked += (s, e) => Edit(f => f.Rescan = "daily");
            rInterval.Checked += (s, e) => Edit(f => f.Rescan = "interval");
            rNever.Checked += (s, e) => Edit(f => f.Rescan = "never");
            cmbTime.SelectionChanged += (s, e) => { if (cmbTime.SelectedItem != null) Edit(f => f.Time = (string)cmbTime.SelectedItem); };
            txtHours.TextChanged += (s, e) =>
            {
                int h;
                if (int.TryParse(txtHours.Text.Trim(), out h) && h >= 1 && h <= 168) Edit(f => f.Hours = h);
            };

            grpFolder.Content = sp;
            return grpFolder;
        }

        // ----- 제외
        UIElement ExcludeGroup()
        {
            var g = Group("제외할 경로 (한 줄에 하나)");
            var sp = new StackPanel();
            txtExclude = new TextBox();
            txtExclude.AcceptsReturn = true;
            txtExclude.Height = 70;
            txtExclude.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            txtExclude.FontFamily = new FontFamily("Consolas, Malgun Gothic");
            txtExclude.Text = string.Join(Environment.NewLine, cfg.SearchExclude);
            txtExclude.LostFocus += (s, e) => Changed();
            sp.Children.Add(txtExclude);
            var tip = Small("%WINDIR%처럼 환경 변수도 쓸 수 있어요. 휴지통, .git, node_modules 같은 폴더는 자동으로 빠져요.");
            tip.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(tip);
            g.Content = sp;
            return g;
        }

        UIElement Buttons()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "확인", MinWidth = 90, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0), IsDefault = false };
            ok.Click += (s, e) => { if (Apply(true)) Window.GetWindow(this).Close(); };
            var cancel = new Button { Content = "취소", MinWidth = 90, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            cancel.Click += (s, e) => Window.GetWindow(this).Close();
            var apply = new Button { Content = "적용", MinWidth = 90, Padding = new Thickness(10, 5, 10, 5) };
            apply.Click += (s, e) => Apply(true);
            row.Children.Add(ok);
            row.Children.Add(cancel);
            row.Children.Add(apply);
            return row;
        }

        // ----- 동작
        void RebuildList(int select)
        {
            loading = true;
            folderList.Items.Clear();
            statusBlocks.Clear();
            foreach (var f in folders)
            {
                var sp = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
                sp.Children.Add(new TextBlock { Text = f.Path, FontWeight = FontWeights.SemiBold, Foreground = Theme.B(Theme.Fg) });
                var st = Small("");
                sp.Children.Add(st);
                statusBlocks.Add(st);
                folderList.Items.Add(sp);
            }
            loading = false;
            folderList.SelectedIndex = Math.Min(select, folders.Count - 1);
            LoadSelected();
            RefreshStatus();
        }

        void LoadSelected()
        {
            if (loading) return;
            int i = folderList.SelectedIndex;
            loading = true;
            folderPanel.IsEnabled = i >= 0;
            if (i >= 0)
            {
                var f = folders[i];
                grpFolder.Header = f.Path + " 설정";
                chkSub.IsChecked = f.Subfolders;
                chkWatch.IsChecked = f.Watch;
                rDaily.IsChecked = f.Rescan == "daily";
                rInterval.IsChecked = f.Rescan == "interval";
                rNever.IsChecked = f.Rescan == "never";
                if (!cmbTime.Items.Contains(f.Time)) cmbTime.Items.Add(f.Time);
                cmbTime.SelectedItem = f.Time;
                txtHours.Text = f.Hours.ToString();
            }
            else
            {
                grpFolder.Header = "폴더 설정 (위 목록에서 폴더를 고르세요)";
            }
            loading = false;
        }

        void Edit(Action<FolderSetting> change)
        {
            if (loading) return;
            int i = folderList.SelectedIndex;
            if (i < 0 || i >= folders.Count) return;
            change(folders[i]);
            Changed();
        }

        void BrowseAdd()
        {
            var dlg = new WinForms.FolderBrowserDialog();
            dlg.Description = "색인할 폴더를 고르세요 (네트워크 > 서버에서 공유 폴더도 고를 수 있어요)";
            dlg.ShowNewFolderButton = false;
            if (dlg.ShowDialog() == WinForms.DialogResult.OK) AddFolder(dlg.SelectedPath);
        }

        void AddFolder(string path)
        {
            path = (path ?? "").Trim().Trim('"');
            if (path.Length == 0) return;
            if (path.Length > 3) path = path.TrimEnd('\\');
            int existing = folders.FindIndex(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) { folderList.SelectedIndex = existing; return; }
            bool exists = false;
            string why = "";
            if (Native.IsServerOnly(path))
            {
                int err;
                var shares = Native.ListShares(path, out err);
                exists = shares.Count > 0;
                if (exists) app.Toast("공유 폴더 " + shares.Count + "개를 찾았어요: " + string.Join(", ", shares.Select(x => IO.Path.GetFileName(x)).Take(4).ToArray()) + (shares.Count > 4 ? " …" : ""), 3500);
                else why = err != 0 ? Native.ShareError(err) : "공유 폴더가 없어요";
            }
            else
            {
                try { exists = IO.Directory.Exists(Environment.ExpandEnvironmentVariables(path)); } catch (Exception) { }
            }
            if (!exists)
            {
                var r = MessageBox.Show(Window.GetWindow(this), "지금은 이 폴더에 연결할 수 없어요.\n" + path + (why.Length > 0 ? "\n(" + why + ")" : "") + "\n\n탐색기에서 한 번 열어 로그인(자격 증명 기억)한 뒤 다시 시도하면 대부분 해결돼요.\n그래도 지금 추가할까요? (연결되면 자동으로 색인해요)", "검색 설정", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }
            var nf = new FolderSetting { Path = path };
            if (!path.StartsWith(@"\\")) { nf.Rescan = "interval"; nf.Hours = 1; }
            folders.Add(nf);
            RebuildList(folders.Count - 1);
            Changed();
        }

        void RemoveSelected()
        {
            int i = folderList.SelectedIndex;
            if (i < 0) return;
            folders.RemoveAt(i);
            RebuildList(Math.Min(i, folders.Count - 1));
            Changed();
        }

        void RescanSelected()
        {
            int i = folderList.SelectedIndex;
            if (i < 0) return;
            Apply(false);
            string p = folders[i].Path;
            if (FileIndex.RescanPath(p)) app.Toast("다시 훑는 중이에요: " + p, 2200);
        }

        public bool Apply(bool notify)
        {
            var fresh = Config.Load(); // 다른 설정(단축키 등)은 그대로 두고
            fresh.IndexLocalDrives = chkLocal.IsChecked == true;
            fresh.IncludeMappedDrives = chkMapped.IsChecked == true;
            fresh.Folders = folders.Select(f => f.Clone()).ToList();
            fresh.SearchExclude = txtExclude.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            try
            {
                fresh.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), "설정을 저장하지 못했어요.\n" + ex.Message, "검색 설정");
                return false;
            }
            cfg = fresh;
            app.Cfg = fresh;
            FileIndex.Reconfigure(fresh);
            if (notify) app.Toast("검색 설정을 저장했어요", 1800);
            RefreshStatus();
            return true;
        }

        void RefreshStatus()
        {
            var local = FileIndex.Find("local");
            lblLocal.Text = local != null ? local.StatusLine() : (chkLocal.IsChecked == true ? "적용을 누르면 색인을 시작해요" : "꺼져 있어요");
            var mapped = FileIndex.Find("mapped");
            if (mapped != null)
                lblMapped.Text = mapped.Roots.Length == 0 ? "연결된 네트워크 드라이브가 없어요" : string.Join(", ", mapped.Roots) + " · " + mapped.StatusLine();
            else
                lblMapped.Text = chkMapped.IsChecked == true ? "적용을 누르면 색인을 시작해요" : "꺼져 있어요";
            for (int i = 0; i < folders.Count && i < statusBlocks.Count; i++)
            {
                var src = FileIndex.Find(folders[i].Path);
                statusBlocks[i].Text = src != null ? src.StatusLine() : "적용을 누르면 색인을 시작해요";
            }
        }
    }

    // ------------------------------------------------------------ 기능 5: 메모 (스티커 메모 / 메모장 열기)
    // ------------------------------------------------------------ 자동 업데이트 (TeamCal 방식)
    // version.json 을 호스팅해 두면, 앱이 켤 때와 30분마다 확인하고 새 버전이면 받아서 다시 빌드합니다.
    static class Updater
    {
        static bool busy;

        static int[] Parse(string v)
        {
            v = (v ?? "").Trim().TrimStart('v', 'V');
            var parts = v.Split('.');
            var n = new int[3];
            for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(new string(parts[i].TakeWhile(char.IsDigit).ToArray()), out n[i]);
            return n;
        }

        static bool IsNewer(string remote, string local)
        {
            var a = Parse(remote); var b = Parse(local);
            for (int i = 0; i < 3; i++) { if (a[i] != b[i]) return a[i] > b[i]; }
            return false;
        }

        static string Fetch(string url)
        {
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072; // TLS 1.2
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers.Add("User-Agent", "ClaudeRing");
                string sep = url.IndexOf('?') >= 0 ? "&" : "?";
                return wc.DownloadString(url + sep + "t=" + DateTime.UtcNow.Ticks);
            }
        }

        public static void Check(RingApp app, bool manual)
        {
            string url = app.EffectiveUpdateUrl;
            if (string.IsNullOrEmpty(url)) { if (manual) app.Toast("업데이트 주소가 비어 있어요. 설정 → 일반 → 업데이트에서 넣어 주세요.", 4000); return; }
            if (busy) return;
            busy = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string ver = null, notes = null, fileUrl = null, error = null;
                try
                {
                    var obj = new JavaScriptSerializer().DeserializeObject(Fetch(url)) as Dictionary<string, object>;
                    object o;
                    if (obj != null)
                    {
                        if (obj.TryGetValue("version", out o)) ver = Convert.ToString(o);
                        if (obj.TryGetValue("notes", out o)) notes = Convert.ToString(o);
                        if (obj.TryGetValue("url", out o)) fileUrl = Convert.ToString(o);
                    }
                    if (string.IsNullOrEmpty(ver)) error = "버전 정보를 읽지 못했어요.";
                    else if (!string.IsNullOrEmpty(fileUrl)) fileUrl = new Uri(new Uri(url), fileUrl).AbsoluteUri;
                }
                catch (Exception ex) { error = ex.Message; RingApp.Log("업데이트 확인 오류: " + ex.Message); }

                string fVer = ver, fNotes = notes, fFile = fileUrl, fErr = error;
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    busy = false;
                    if (fErr != null) { if (manual) app.Toast("업데이트를 확인하지 못했어요: " + fErr, 4000); return; }
                    if (!IsNewer(fVer, RingApp.APP_VERSION))
                    {
                        app.UpdateAvailable = false;
                        if (manual) app.Toast("최신 버전이에요 (v" + RingApp.APP_VERSION + ")", 2400);
                        return;
                    }
                    bool first = !app.UpdateAvailable;
                    app.UpdateAvailable = true;
                    app.UpdateVersion = fVer;
                    app.UpdateNotes = fNotes;
                    app.UpdateFileUrl = fFile;
                    if (manual) PromptAndApply(app);
                    else if (first) app.Toast("새 버전 v" + fVer + " 이 있어요 · 링 설정 톱니바퀴(!)에서 업데이트", 4000);
                }));
            });
        }

        public static void PromptAndApply(RingApp app)
        {
            if (!app.UpdateAvailable) { Check(app, true); return; }
            string msg = "새 버전 v" + app.UpdateVersion + " 이 있어요.";
            if (!string.IsNullOrEmpty(app.UpdateNotes)) msg += "\n\n" + app.UpdateNotes;
            msg += "\n\n지금 업데이트할까요? (잠깐 창이 닫혔다가 새 버전으로 다시 켜져요)";
            var r = MessageBox.Show(msg, "Claude 액션링 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r != MessageBoxResult.Yes) return;
            if (string.IsNullOrEmpty(app.UpdateFileUrl)) { app.Toast("내려받을 파일 주소가 없어요.", 3500); return; }
            DoUpdate(app, app.UpdateFileUrl);
        }

        static void DoUpdate(RingApp app, string fileUrl)
        {
            app.Toast("새 버전을 받는 중…", 3000);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string error = null;
                try
                {
                    string text = Fetch(fileUrl);
                    if (text == null || text.IndexOf("ClaudeRing-Source", StringComparison.Ordinal) < 0)
                        { error = "받은 파일이 올바르지 않아요."; }
                    else
                    {
                        string dir = AppDomain.CurrentDomain.BaseDirectory;
                        string cs = IO.Path.Combine(dir, "ClaudeRing.cs");
                        string bat = IO.Path.Combine(dir, "build.bat");
                        if (!IO.File.Exists(bat)) { error = "build.bat 이 없어서 다시 빌드할 수 없어요. 폴더에 build.bat 을 두세요."; }
                        else
                        {
                            IO.File.WriteAllText(cs, text, new UTF8Encoding(false));
                            app.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { Process.Start(new ProcessStartInfo(bat) { UseShellExecute = true, WorkingDirectory = dir }); }
                                catch (Exception ex) { app.Toast("빌드를 시작하지 못했어요: " + ex.Message, 4000); }
                            }));
                            return;
                        }
                    }
                }
                catch (Exception ex) { error = ex.Message; RingApp.Log("업데이트 적용 오류: " + ex.Message); }
                string fe = error;
                app.Dispatcher.BeginInvoke(new Action(() => app.Toast("업데이트 실패: " + fe, 4500)));
            });
        }
    }

    static class Memo
    {
        const string STICKY_AUMID = "Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe!App";
        static DispatcherTimer poll;

        public static void Open(RingApp app)
        {
            bool sticky = app.Cfg.MemoTarget != "notepad";
            string[] keys = sticky ? new[] { "스티커", "Sticky" } : new[] { "메모장", "Notepad" };
            IntPtr before = Native.GetForegroundWindow();
            try
            {
                if (sticky) Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + STICKY_AUMID) { UseShellExecute = true });
                else Process.Start("notepad.exe");
            }
            catch (Exception ex)
            {
                RingApp.Log("메모 열기 실패: " + ex.Message);
                if (sticky) { try { Process.Start("notepad.exe"); } catch (Exception) { app.Toast("메모를 열지 못했어요", 3000); } }
                else app.Toast("메모장을 열지 못했어요: " + ex.Message, 3500);
                return;
            }

            // 메모 창이 뜨면: (스티커 메모면) 새 메모를 만들고, 설정한 모니터/모서리로 옮김
            bool newNote = sticky && app.Cfg.MemoNewNote;
            if (poll != null) poll.Stop();
            DateTime until = DateTime.Now.AddSeconds(4);
            poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            poll.Tick += (s, e) =>
            {
                if (DateTime.Now > until) { poll.Stop(); return; }
                IntPtr h = Native.GetForegroundWindow();
                if (h == IntPtr.Zero || h == before) return;
                var sb = new StringBuilder(256);
                Native.GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString();
                bool match = false;
                foreach (var k in keys) if (title.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) match = true;
                if (!match) return;
                poll.Stop();
                if (newNote) Native.SendCtrlN();
                // 새 메모가 뜰 시간을 조금 준 뒤 위치 조정
                var t2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(newNote ? 400 : 60) };
                t2.Tick += (s2, e2) =>
                {
                    t2.Stop();
                    IntPtr hw = Native.GetForegroundWindow();
                    app.PlaceExternalWindow(hw != IntPtr.Zero ? hw : h);
                };
                t2.Start();
            };
            poll.Start();
        }
    }

    // ------------------------------------------------------------ 기능 4: Claude에게 물어보기 (내 계정 로그인)
    // API 키 없이, 로그인되어 있는 Claude 앱(또는 claude.ai)에 질문을 넘겨서 엽니다.
    class AskWindow : Window
    {
        static AskWindow instance;
        RingApp app;
        Color accent;
        TextBox box;
        TextBlock placeholder;
        CheckBox clip;

        public static void Open(RingApp a)
        {
            if (instance != null) { instance.Activate(); return; }
            var w = new AskWindow(a);
            instance = w;
            w.Closed += (s, e) => { if (instance == w) instance = null; };
            w.Show();
            w.Activate();
            Native.SetForegroundWindow(new WindowInteropHelper(w).Handle);
            w.box.Focus();
        }

        AskWindow(RingApp a)
        {
            app = a;
            accent = app.AccentColor;
            Title = "물어보기 - Claude 액션링";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = Theme.Font;
            FontSize = 13;
            Resources = Theme.Styles(accent);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var card = new Border
            {
                Width = 460,
                Margin = new Thickness(18),
                CornerRadius = new CornerRadius(18),
                Background = Theme.B(Theme.Bg),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 18, 20, 16),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = 0.28, Direction = 270 }
            };
            card.LayoutTransform = new ScaleTransform(app.UiScale, app.UiScale);
            var sp = new StackPanel();
            card.Child = sp;
            Content = card;

            // 제목
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 14), Background = Brushes.Transparent };
            head.MouseLeftButtonDown += (s, e) => { if (!e.Handled && e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch (Exception) { } } };
            var badge = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Theme.B(accent) };
            badge.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(badge, Dock.Left);
            head.Children.Add(badge);
            var close = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            close.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Foreground = Theme.B(Theme.Muted), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            close.MouseEnter += (s, e) => close.Background = Theme.B(Theme.Sel);
            close.MouseLeave += (s, e) => close.Background = Brushes.Transparent;
            close.MouseLeftButtonDown += (s, e) => { e.Handled = true; Close(); };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            var titles = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = "Claude에게 물어보기", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg) });
            titles.Children.Add(new TextBlock { Text = (app.Cfg.AskTarget == "web" ? "claude.ai" : "내 Claude 앱") + "에서 열려요 · 내 계정 로그인", FontSize = 11.5, Foreground = Theme.B(Theme.Muted) });
            head.Children.Add(titles);
            sp.Children.Add(head);

            // 입력
            var boxWrap = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.5), BorderBrush = Theme.B(accent), Background = Theme.B(Theme.Field) };
            var grid = new Grid();
            box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinLines = 3, MaxLines = 10, FontSize = 16, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(12, 10, 12, 10), Foreground = Theme.B(Theme.Fg) };
            placeholder = new TextBlock { Text = "무엇이든 물어보세요", FontSize = 16, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(14, 11, 12, 10), IsHitTestVisible = false };
            box.TextChanged += (s, e) => placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; Send(); }
                else if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            };
            grid.Children.Add(box);
            grid.Children.Add(placeholder);
            boxWrap.Child = grid;
            sp.Children.Add(boxWrap);

            // 아래 줄
            var foot = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var send = new Button { Content = "보내기", Height = 36, Padding = new Thickness(18, 0, 18, 0), Background = Theme.B(Theme.Fg), Foreground = Theme.B(Theme.Bg), Cursor = Cursors.Hand };
            send.Click += (s, e) => Send();
            DockPanel.SetDock(send, Dock.Right);
            foot.Children.Add(send);
            clip = new CheckBox { IsChecked = app.Cfg.AskIncludeClipboard, Content = new TextBlock { Text = "복사해 둔 텍스트 함께 보내기", Foreground = Theme.B(Theme.Fg), FontSize = 12.5 }, VerticalAlignment = VerticalAlignment.Center };
            foot.Children.Add(clip);
            sp.Children.Add(foot);
            sp.Children.Add(new TextBlock { Text = "Enter 보내기 · Shift+Enter 줄바꿈", FontSize = 11, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(2, 8, 0, 0) });

            Loaded += (s, e) =>
            {
                var tl = app.PlaceInMonitor(app.LastPoint, ActualWidth, ActualHeight);
                Left = tl.X; Top = tl.Y;
            };
            Deactivated += (s, e) => { /* 입력 중 실수로 닫히지 않게 유지 */ };
        }

        void Send()
        {
            string q = (box.Text ?? "").Trim();
            if (q.Length == 0) { Close(); return; }
            if (clip.IsChecked == true)
            {
                string c = "";
                try { if (System.Windows.Clipboard.ContainsText()) c = System.Windows.Clipboard.GetText().Trim(); } catch (Exception) { }
                if (c.Length > 0) q = q + "\n\n---\n참고 텍스트:\n" + c;
            }
            // 설정 저장 (클립보드 옵션 기억)
            try { var f = Config.Load(); f.AskIncludeClipboard = clip.IsChecked == true; f.Save(); app.Cfg = f; } catch (Exception) { }

            if (q.Length > 13500) q = q.Substring(0, 13500); // q 파라미터 약 14,000자 제한
            string enc = Uri.EscapeDataString(q);
            bool web = app.Cfg.AskTarget == "web";
            if (!web)
            {
                // 내 계정으로 로그인된 Claude 앱에서 새 대화를 열고 질문을 미리 채움
                try { Process.Start(new ProcessStartInfo("claude://claude.ai/new?q=" + enc) { UseShellExecute = true }); Close(); return; }
                catch (Exception ex) { RingApp.Log("Claude 앱 열기 실패, 웹으로 전환: " + ex.Message); }
            }
            try { Process.Start(new ProcessStartInfo("https://claude.ai/new?q=" + enc) { UseShellExecute = true }); }
            catch (Exception ex) { app.Toast("Claude를 열지 못했어요: " + ex.Message, 4000); }
            Close();
        }
    }


    // ------------------------------------------------------------ 기능 3: 화면 캡처 (Win+Shift+S + 자동 저장)
    static class Capture
    {
        static DispatcherTimer watch;
        static RingApp app;

        public static string DefaultFolder()
        {
            string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return IO.Path.Combine(pics, "ClaudeRing 캡처");
        }

        public static string Folder(Config cfg)
        {
            string f = (cfg.CaptureFolder ?? "").Trim();
            return f.Length > 0 ? f : DefaultFolder();
        }

        public static void Start(RingApp a)
        {
            app = a;
            // 이미 지켜보는 중이면 중복 실행 방지
            if (watch != null && watch.IsEnabled) { Native.SendWinShiftS(); return; }

            uint before = Native.GetClipboardSequenceNumber();
            Native.SendWinShiftS();

            if (!app.Cfg.CaptureAutoSave)
            {
                app.Toast("영역을 끌어서 캡처하세요 · 클립보드에 복사돼요", 2600);
                return;
            }

            app.Toast("영역을 끌어서 캡처하세요 · 저장해 드릴게요", 2600);
            DateTime until = DateTime.Now.AddSeconds(120);
            watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            watch.Tick += (s, e) =>
            {
                if (DateTime.Now > until) { watch.Stop(); return; }
                uint now = Native.GetClipboardSequenceNumber();
                if (now == before) return;
                // 클립보드가 바뀜 — 이미지인지 확인
                System.Windows.Media.Imaging.BitmapSource img = null;
                try { if (System.Windows.Clipboard.ContainsImage()) img = System.Windows.Clipboard.GetImage(); }
                catch (Exception) { return; } // 아직 다른 앱이 클립보드를 잡고 있으면 다음 틱에 다시
                if (img == null) { before = now; return; } // 이미지가 아니면(텍스트 복사 등) 무시하고 계속 지켜봄
                watch.Stop();
                SaveImage(img);
            };
            watch.Start();
        }

        static void SaveImage(System.Windows.Media.Imaging.BitmapSource img)
        {
            try
            {
                string dir = Folder(app.Cfg);
                IO.Directory.CreateDirectory(dir);
                bool jpg = (app.Cfg.CaptureFormat ?? "png").ToLowerInvariant() == "jpg";
                string ext = jpg ? ".jpg" : ".png";
                string name = "캡처_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
                string path = IO.Path.Combine(dir, name);
                System.Windows.Media.Imaging.BitmapEncoder enc;
                if (jpg)
                {
                    // JPG는 투명을 지원하지 않으므로 흰 배경에 얹음
                    var dv = new System.Windows.Media.DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, img.PixelWidth, img.PixelHeight));
                        dc.DrawImage(img, new Rect(0, 0, img.PixelWidth, img.PixelHeight));
                    }
                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(img.PixelWidth, img.PixelHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(dv);
                    enc = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 92 };
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                }
                else
                {
                    enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(img));
                }
                using (var fs = new IO.FileStream(path, IO.FileMode.Create))
                    enc.Save(fs);
                ShowSaved(path, name);
            }
            catch (Exception ex)
            {
                RingApp.Log("캡처 저장 오류: " + ex);
                app.Toast("캡처는 클립보드에 있어요 · 저장 중 오류: " + ex.Message, 4000);
            }
        }

        // 저장 후 작은 알림 카드 (폴더 열기 / 복사는 이미 됨)
        static void ShowSaved(string path, string name)
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                FontFamily = Theme.Font
            };
            var card = new Border
            {
                Background = Theme.B(Theme.Bg),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(14),
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 5, Opacity = 0.25, Direction = 270 }
            };
            card.LayoutTransform = new ScaleTransform(app.UiScale, app.UiScale);
            var sp = new StackPanel { Width = 300 };
            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var dot = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Theme.B(app.AccentColor), Margin = new Thickness(0, 0, 8, 0) };
            dot.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            top.Children.Add(dot);
            top.Children.Add(new TextBlock { Text = "캡처를 저장했어요", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(top);
            sp.Children.Add(new TextBlock { Text = name + " · 클립보드에도 복사됨", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 0, 0, 10), TextTrimming = TextTrimming.CharacterEllipsis });
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Func<string, bool, Button> mk = (txt, primary) =>
            {
                var b = new Button { Content = txt, Height = 30, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand };
                if (primary) { b.Background = Theme.B(Theme.Fg); b.Foreground = Theme.B(Theme.Bg); } else { b.Background = Brushes.Transparent; b.Foreground = Theme.B(Theme.Fg); b.BorderBrush = Theme.B(Theme.Line); b.BorderThickness = new Thickness(1); }
                return b;
            };
            var openFolder = mk("폴더 열기", false);
            openFolder.Click += (s, e) => { try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch (Exception) { } w.Close(); };
            var openImg = mk("열기", false);
            openImg.Click += (s, e) => { try { Process.Start(path); } catch (Exception) { } w.Close(); };
            var close = mk("닫기", true);
            close.Click += (s, e) => w.Close();
            row.Children.Add(openFolder);
            row.Children.Add(openImg);
            row.Children.Add(close);
            sp.Children.Add(row);
            card.Child = sp;
            w.Content = card;
            System.Windows.Point cc = app.LastPoint;
            w.Loaded += (s, e) =>
            {
                var wa = app.WorkAreaAt(cc);
                w.Left = wa.Right - w.ActualWidth - 20;
                w.Top = wa.Bottom - w.ActualHeight - 20;
            };
            w.Show();
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            t.Tick += (s, e) => { t.Stop(); try { w.Close(); } catch (Exception) { } };
            t.Start();
        }
    }


    // ------------------------------------------------------------ 기능 2: 파일 정리 (매번 정리 방법을 물어봄)
    class OrganizeWindow : Window
    {
        static OrganizeWindow instance;

        class Move
        {
            public string From;
            public string Folder; // 대상 폴더 이름 (정리할 폴더 안)
            public bool OldVersion;
        }

        static readonly string[][] SCHEMES = {
            new[] { "kind", "종류별", "도면 · PDF · 문서 · 이미지 · 3D · 압축 · 기타" },
            new[] { "project", "프로젝트 표준", "01_계약 · 02_기획 · 03_계획설계 · 04_인허가 · 05_실시설계 · 06_감리 · 07_사진 · 99_기타 (파일 이름으로 판단)" },
            new[] { "date", "날짜별", "수정한 날짜 기준 2026-11 같은 월별 폴더" },
            new[] { "ext", "확장자별", "DWG · PDF · XLSX 처럼 확장자 이름 폴더" }
        };

        static readonly Dictionary<string, string> KIND = BuildKind();
        static Dictionary<string, string> BuildKind()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in new[] { "dwg", "dxf", "dgn", "dwf", "bak" }) d[e] = "도면";
            d["pdf"] = "PDF";
            foreach (var e in new[] { "hwp", "hwpx", "doc", "docx", "xls", "xlsx", "xlsm", "ppt", "pptx", "txt", "csv", "rtf" }) d[e] = "문서";
            foreach (var e in new[] { "jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "heic", "webp", "psd", "ai" }) d[e] = "이미지";
            foreach (var e in new[] { "skp", "3dm", "rvt", "rfa", "max", "ifc", "obj", "fbx", "3ds", "lumion" }) d[e] = "3D";
            foreach (var e in new[] { "zip", "7z", "rar", "egg", "alz", "tar", "gz" }) d[e] = "압축";
            return d;
        }

        static readonly string[][] PROJECT_RULES = {
            new[] { "01_계약", "계약", "견적", "용역", "발주", "청구", "세금계산서" },
            new[] { "02_기획", "기획", "개요", "사업", "타당성", "규모검토", "제안" },
            new[] { "03_계획설계", "계획", "스케치", "매스", "컨셉", "기본설계", "배치" },
            new[] { "04_인허가", "인허가", "허가", "협의", "심의", "신고", "승인", "변경사유" },
            new[] { "05_실시설계", "실시", "시방", "상세", "구조", "설비", "전기", "내역" },
            new[] { "06_감리", "감리", "현장", "공정", "점검", "준공" }
        };

        static readonly System.Text.RegularExpressions.Regex VERSION = new System.Text.RegularExpressions.Regex(
            @"(진짜)?최종\d*|final\d*|[_\-\s]?v\d+(\.\d+)?|[_\-\s]?rev\.?\d+|[_\-\s]r\d+$|수정본?\d*|\(\d+\)|사본|copy|[_\-\s]+\d{6,8}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        RingApp app;
        Color accent;
        string scheme;          // 아직 안 고르면 null → 매번 직접 고르게
        TextBox txtFolder;
        WrapPanel recentPanel;
        List<Border> cards = new List<Border>();
        CheckBox chkOld, chkSub;
        StackPanel previewList;
        TextBlock previewTitle;
        Button btnGo;
        StackPanel content;
        TextBlock sourceNote;
        DispatcherTimer pickTimer;
        List<Move> plan = new List<Move>();
        int version;
        DispatcherTimer debounce;

        public static void Open(RingApp app)
        {
            if (instance != null) { instance.Activate(); return; }
            var w = new OrganizeWindow(app);
            instance = w;
            w.Closed += (s, e) => { if (instance == w) instance = null; };
            w.Show();
            w.Activate();
        }

        OrganizeWindow(RingApp owner)
        {
            app = owner;
            accent = app.AccentColor;
            Title = "파일 정리 - Claude 액션링";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = Theme.Font;
            FontSize = 13;
            Resources = Theme.Styles(accent);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            debounce.Tick += (s, e) => { debounce.Stop(); BuildPlan(); };

            var card = new Border
            {
                Width = 720,
                Margin = new Thickness(18),
                CornerRadius = new CornerRadius(20),
                Background = Theme.B(Theme.Bg),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 20, 20, 16),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = 0.28, Direction = 270 }
            };
            card.LayoutTransform = new ScaleTransform(app.UiScale, app.UiScale);
            var root = new StackPanel();
            card.Child = root;
            Content = card;

            root.Children.Add(Header("파일 정리", "정리할 폴더와 정리 방법을 고르면 미리 보여 드려요"));
            content = new StackPanel();
            root.Children.Add(content);
            BuildAsk();

            Loaded += (s, e) =>
            {
                var tl = app.PlaceInMonitor(app.LastPoint, ActualWidth, ActualHeight);
                Left = tl.X; Top = tl.Y;
            };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Closed += (s, e) => { debounce.Stop(); if (pickTimer != null) pickTimer.Stop(); };
        }

        // ----- 부품
        UIElement Header(string title, string sub)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 16), Background = Brushes.Transparent };
            row.MouseLeftButtonDown += (s, e) => { if (!e.Handled && e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch (Exception) { } } };
            var badge = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Theme.B(accent) };
            badge.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge);
            var close = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "닫기 (Esc)" };
            close.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Foreground = Theme.B(Theme.Muted), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            close.MouseEnter += (s, e) => close.Background = Theme.B(Theme.Sel);
            close.MouseLeave += (s, e) => close.Background = Brushes.Transparent;
            close.MouseLeftButtonDown += (s, e) => { e.Handled = true; Close(); };
            DockPanel.SetDock(close, Dock.Right);
            row.Children.Add(close);
            var gear = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "파일 정리 설정", Margin = new Thickness(0, 0, 2, 0) };
            gear.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, Foreground = Theme.B(Theme.Muted), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            gear.MouseEnter += (s, e) => gear.Background = Theme.B(Theme.Sel);
            gear.MouseLeave += (s, e) => gear.Background = Brushes.Transparent;
            gear.MouseLeftButtonDown += (s, e) => { e.Handled = true; Close(); app.OpenSettings("organize"); };
            DockPanel.SetDock(gear, Dock.Right);
            row.Children.Add(gear);
            var titles = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg) });
            titles.Children.Add(new TextBlock { Text = sub, FontSize = 11.5, Foreground = Theme.B(Theme.Muted) });
            row.Children.Add(titles);
            return row;
        }

        static TextBlock Grp(string t)
        {
            return new TextBlock { Text = t, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 0, 0, 8) };
        }

        static TextBlock Small(string t)
        {
            return new TextBlock { Text = t, FontSize = 11.5, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap };
        }

        // ----- 1단계: 묻기 (폴더 · 방법 · 옵션 · 미리보기)
        void BuildAsk()
        {
            content.Children.Clear();
            var cfg = app.Cfg;

            // 정리할 폴더
            content.Children.Add(Grp("1  정리할 폴더"));
            var row = new DockPanel();
            var browse = new Button { Content = "찾아보기…", Height = 34, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(8, 0, 0, 0) };
            browse.Click += (s, e) =>
            {
                var dlg = new WinForms.FolderBrowserDialog { Description = "정리할 폴더를 고르세요", ShowNewFolderButton = false };
                if (IO.Directory.Exists(txtFolder.Text)) dlg.SelectedPath = txtFolder.Text;
                if (dlg.ShowDialog() == WinForms.DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
            };
            DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse);
            var pick = new Button { Height = 34, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(8, 0, 0, 0), ToolTip = "누른 다음 정리할 탐색기 창을 클릭하세요" };
            var pickContent = new StackPanel { Orientation = Orientation.Horizontal };
            pickContent.Children.Add(new TextBlock { Text = "\uE7C9", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            pickContent.Children.Add(new TextBlock { Text = "탐색기 창 클릭", VerticalAlignment = VerticalAlignment.Center });
            pick.Content = pickContent;
            pick.Click += (s, e) => PickExplorer();
            DockPanel.SetDock(pick, Dock.Right);
            row.Children.Add(pick);
            txtFolder = new TextBox { Height = 34, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13.5 };
            txtFolder.TextChanged += (s, e) => Changed();
            row.Children.Add(txtFolder);
            content.Children.Add(row);

            sourceNote = new TextBlock { FontSize = 11.5, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(2, 6, 0, 0) };
            content.Children.Add(sourceNote);
            recentPanel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            content.Children.Add(recentPanel);
            FillRecent();

            string start = app.ExplorerFolder();
            if (!string.IsNullOrEmpty(start)) sourceNote.Text = "링을 연 자리의 탐색기 폴더를 가져왔어요";
            else if (cfg.OrganizeRecent.Length > 0) { start = cfg.OrganizeRecent[0]; sourceNote.Text = "마지막으로 정리한 폴더예요 · 다른 폴더는 [탐색기 창 클릭]으로 골라요"; }
            else sourceNote.Text = "[탐색기 창 클릭]을 누르고 정리할 탐색기 창을 클릭하거나, 경로를 넣어 주세요";
            txtFolder.Text = start ?? "";

            // 정리 방법 (매번 직접 고름)
            var g2 = Grp("2  정리 방법 · 매번 골라요");
            g2.Margin = new Thickness(0, 18, 0, 8);
            content.Children.Add(g2);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cards.Clear();
            for (int i = 0; i < SCHEMES.Length; i++)
            {
                string id = SCHEMES[i][0];
                var c = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1.5), Padding = new Thickness(14, 10, 14, 11), Margin = new Thickness(i % 2 == 0 ? 0 : 5, 0, i % 2 == 0 ? 5 : 0, 10), Cursor = Cursors.Hand, Tag = id };
                var sp = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal };
                top.Children.Add(new TextBlock { Text = SCHEMES[i][1], FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Theme.B(Theme.Fg) });
                if (id == cfg.OrganizeLastScheme)
                    top.Children.Add(new Border { Margin = new Thickness(8, 1, 0, 0), Padding = new Thickness(6, 0, 6, 1), CornerRadius = new CornerRadius(8), Background = Theme.B(Theme.Sel), Child = new TextBlock { Text = "지난번", FontSize = 10.5, Foreground = Theme.B(Theme.Muted) } });
                sp.Children.Add(top);
                sp.Children.Add(new TextBlock { Text = SCHEMES[i][2], FontSize = 11.5, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                c.Child = sp;
                c.MouseLeftButtonUp += (s, e) => { scheme = id; PaintCards(); Changed(); };
                Grid.SetRow(c, i / 2);
                Grid.SetColumn(c, i % 2);
                cards.Add(c);
                grid.Children.Add(c);
            }
            content.Children.Add(grid);
            PaintCards();

            // 옵션
            var g3 = Grp("3  옵션");
            g3.Margin = new Thickness(0, 8, 0, 4);
            content.Children.Add(g3);
            chkOld = new CheckBox { IsChecked = cfg.OrganizeOldVersions, Content = new TextBlock { Text = "같은 파일의 옛 버전(최종, 최종2, v1, 수정 …)은 _old 폴더로", Foreground = Theme.B(Theme.Fg) }, Margin = new Thickness(0, 4, 0, 6) };
            chkSub = new CheckBox { IsChecked = cfg.OrganizeSubfolders, Content = new TextBlock { Text = "하위 폴더에 있는 파일도 정리", Foreground = Theme.B(Theme.Fg) } };
            chkOld.Click += (s, e) => Changed();
            chkSub.Click += (s, e) => Changed();
            content.Children.Add(chkOld);
            content.Children.Add(chkSub);

            // 미리보기
            var g4 = Grp("4  미리보기");
            g4.Margin = new Thickness(0, 18, 0, 8);
            content.Children.Add(g4);
            var box = new Border { CornerRadius = new CornerRadius(12), BorderBrush = Theme.B(Theme.Line), BorderThickness = new Thickness(1), Background = Theme.B(Theme.Field), Padding = new Thickness(12, 10, 12, 10) };
            var bsp = new StackPanel();
            previewTitle = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Theme.B(Theme.Fg), TextWrapping = TextWrapping.Wrap };
            previewList = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            bsp.Children.Add(previewTitle);
            bsp.Children.Add(new ScrollViewer { Content = previewList, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            box.Child = bsp;
            content.Children.Add(box);

            // 아래 버튼
            var foot = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(right, Dock.Right);
            var cancel = new Button { Content = "취소", Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (s, e) => Close();
            btnGo = new Button { Content = "정리하기", Height = 36, Padding = new Thickness(18, 0, 18, 0), Background = Theme.B(Theme.Fg), Foreground = Theme.B(Theme.Bg), IsEnabled = false };
            btnGo.Click += (s, e) => Apply();
            right.Children.Add(cancel);
            right.Children.Add(btnGo);
            foot.Children.Add(right);
            foot.Children.Add(new TextBlock { Text = "파일은 지우지 않고 옮기기만 해요 · 끝나면 한 번에 되돌릴 수 있어요", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(foot);

            Changed();
        }

        // 창을 잠시 숨기고, 사용자가 클릭한 탐색기 창의 폴더를 가져옴 (Esc 취소, 20초 제한)
        void PickExplorer()
        {
            Hide();
            app.Toast("정리할 탐색기 창을 클릭하세요 · Esc 취소", 2600);
            bool wasDown = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
            DateTime until = DateTime.Now.AddSeconds(20);
            if (pickTimer != null) pickTimer.Stop();
            pickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            pickTimer.Tick += (s, e) =>
            {
                bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
                bool esc = (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0;
                if (esc || DateTime.Now > until)
                {
                    pickTimer.Stop();
                    ShowBack(esc ? null : "시간이 지나서 취소했어요");
                    return;
                }
                if (wasDown && !down)
                {
                    // 클릭이 끝난 순간 마우스 아래 창 확인
                    var h = Native.WindowUnderCursor();
                    string path = app.ExplorerFolderFor(h);
                    if (path != null)
                    {
                        pickTimer.Stop();
                        txtFolder.Text = path;
                        sourceNote.Text = "클릭한 탐색기 창의 폴더예요";
                        ShowBack(null);
                        return;
                    }
                    app.Toast("탐색기 창이 아니에요 · 정리할 폴더가 열린 탐색기 창을 클릭하세요", 2000);
                }
                wasDown = down;
            };
            pickTimer.Start();
        }

        void ShowBack(string toast)
        {
            Show();
            Activate();
            if (toast != null) app.Toast(toast, 1800);
        }

        void FillRecent()
        {
            recentPanel.Children.Clear();
            var recent = app.Cfg.OrganizeRecent.Where(x => !string.IsNullOrEmpty(x)).Take(5).ToList();
            if (recent.Count == 0) { recentPanel.Visibility = Visibility.Collapsed; return; }
            recentPanel.Children.Add(new TextBlock { Text = "최근", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            foreach (var r in recent)
            {
                string path = r;
                string name = IO.Path.GetFileName(path.TrimEnd('\\'));
                if (string.IsNullOrEmpty(name)) name = path;
                var chip = new Border { Height = 26, Padding = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(1), BorderBrush = Theme.B(Theme.Line), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, ToolTip = path };
                chip.Child = new TextBlock { Text = name, FontSize = 12, Foreground = Theme.B(Theme.Fg), VerticalAlignment = VerticalAlignment.Center };
                chip.MouseLeftButtonUp += (s, e) => txtFolder.Text = path;
                recentPanel.Children.Add(chip);
            }
        }

        void PaintCards()
        {
            foreach (var c in cards)
            {
                bool on = (string)c.Tag == scheme;
                c.BorderBrush = Theme.B(on ? accent : Theme.Line);
                c.Background = on ? Theme.B(Color.FromArgb(0x18, accent.R, accent.G, accent.B)) : Theme.B(Theme.Field);
            }
        }

        void Changed()
        {
            if (debounce == null || previewTitle == null) return;
            debounce.Stop();
            debounce.Start();
        }

        // ----- 분류
        static string NormalizeBase(string fileName)
        {
            string b = IO.Path.GetFileNameWithoutExtension(fileName);
            string n = VERSION.Replace(b, "").Trim(' ', '_', '-', '.').ToLowerInvariant();
            return n.Length == 0 ? b.ToLowerInvariant() : n;
        }

        static string Classify(string schemeId, string path)
        {
            string name = IO.Path.GetFileName(path);
            string ext = IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            if (schemeId == "ext") return ext.Length == 0 ? "확장자없음" : ext.ToUpperInvariant();
            if (schemeId == "date")
            {
                try { return IO.File.GetLastWriteTime(path).ToString("yyyy-MM"); } catch (Exception) { return "날짜모름"; }
            }
            if (schemeId == "project")
            {
                foreach (var rule in PROJECT_RULES)
                    for (int k = 1; k < rule.Length; k++)
                        if (name.IndexOf(rule[k], StringComparison.OrdinalIgnoreCase) >= 0) return rule[0];
                string kind;
                if (KIND.TryGetValue(ext, out kind) && kind == "이미지") return "07_사진";
                return "99_기타";
            }
            string k2;
            return KIND.TryGetValue(ext, out k2) ? k2 : "기타";
        }

        static HashSet<string> TargetNames(string schemeId)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "_old" };
            if (schemeId == "kind") foreach (var v in new[] { "도면", "PDF", "문서", "이미지", "3D", "압축", "기타" }) set.Add(v);
            if (schemeId == "project") { foreach (var r in PROJECT_RULES) set.Add(r[0]); set.Add("07_사진"); set.Add("99_기타"); }
            return set;
        }

        void BuildPlan()
        {
            string folder = (txtFolder.Text ?? "").Trim().Trim('"');
            string sid = scheme;
            bool old = chkOld.IsChecked == true, sub = chkSub.IsChecked == true;
            btnGo.IsEnabled = false;
            previewList.Children.Clear();
            if (folder.Length == 0) { previewTitle.Text = "정리할 폴더를 넣어 주세요"; return; }
            if (sid == null) { previewTitle.Text = "정리 방법을 골라 주세요"; return; }
            previewTitle.Text = "폴더를 살펴보는 중…";
            int v = ++version;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string error = null;
                var moves = new List<Move>();
                try
                {
                    if (!IO.Directory.Exists(folder)) error = "폴더를 찾을 수 없어요";
                    else
                    {
                        var targets = TargetNames(sid);
                        var files = new List<string>();
                        var stack = new Stack<string>();
                        stack.Push(folder);
                        while (stack.Count > 0 && files.Count < 20000)
                        {
                            string d = stack.Pop();
                            try
                            {
                                foreach (var f in IO.Directory.GetFiles(d))
                                {
                                    var a = IO.File.GetAttributes(f);
                                    if ((a & (IO.FileAttributes.Hidden | IO.FileAttributes.System)) != 0) continue;
                                    string n = IO.Path.GetFileName(f);
                                    if (n.StartsWith("~$") || n.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                                    files.Add(f);
                                }
                                if (sub)
                                    foreach (var sd in IO.Directory.GetDirectories(d))
                                    {
                                        string dn = IO.Path.GetFileName(sd);
                                        if (d == folder && (targets.Contains(dn) || (sid == "date" && System.Text.RegularExpressions.Regex.IsMatch(dn, @"^\d{4}-\d{2}$")) || (sid == "ext" && dn.ToUpperInvariant() == dn))) continue;
                                        if ((IO.File.GetAttributes(sd) & (IO.FileAttributes.Hidden | IO.FileAttributes.System | IO.FileAttributes.ReparsePoint)) != 0) continue;
                                        stack.Push(sd);
                                    }
                            }
                            catch (Exception) { }
                        }

                        // 옛 버전 찾기: 이름에서 최종/v2/날짜 등을 뗀 뒤 같은 이름+확장자끼리 묶고, 가장 최근 것만 남김
                        var oldSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (old)
                        {
                            foreach (var grp in files.GroupBy(x => NormalizeBase(x) + "|" + IO.Path.GetExtension(x).ToLowerInvariant()))
                            {
                                var list = grp.ToList();
                                if (list.Count < 2) continue;
                                var newest = list.OrderByDescending(y => { try { return IO.File.GetLastWriteTime(y); } catch (Exception) { return DateTime.MinValue; } }).First();
                                foreach (var f in list) if (f != newest) oldSet.Add(f);
                            }
                        }

                        foreach (var f in files)
                        {
                            bool isOld = oldSet.Contains(f);
                            string target = isOld ? "_old" : Classify(sid, f);
                            string destDir = IO.Path.Combine(folder, target);
                            if (string.Equals(IO.Path.GetDirectoryName(f).TrimEnd('\\'), destDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
                            moves.Add(new Move { From = f, Folder = target, OldVersion = isOld });
                        }
                    }
                }
                catch (Exception ex) { error = ex.Message; }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (v != version) return;
                    plan = moves;
                    ShowPlan(error);
                }));
            });
        }

        void ShowPlan(string error)
        {
            previewList.Children.Clear();
            if (error != null) { previewTitle.Text = error; btnGo.IsEnabled = false; return; }
            if (plan.Count == 0) { previewTitle.Text = "옮길 파일이 없어요. 이미 정리되어 있어요."; btnGo.IsEnabled = false; return; }
            var groups = plan.GroupBy(m => m.Folder).OrderBy(g => g.Key == "_old" ? "~" : g.Key).ToList();
            previewTitle.Text = "파일 " + plan.Count.ToString("N0") + "개를 폴더 " + groups.Count + "개로 옮길게요";
            foreach (var g in groups)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
                var name = new TextBlock { Text = (g.Key == "_old" ? "_old (옛 버전)" : g.Key), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Theme.B(Theme.Fg) };
                var files = new TextBlock { Text = string.Join(" · ", g.Take(4).Select(m => IO.Path.GetFileName(m.From)).ToArray()) + (g.Count() > 4 ? " …" : ""), FontSize = 12, Foreground = Theme.B(Theme.Muted), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 8, 0) };
                files.ToolTip = string.Join("\n", g.Take(30).Select(m => IO.Path.GetFileName(m.From)).ToArray()) + (g.Count() > 30 ? "\n…" : "");
                var count = new TextBlock { Text = g.Count().ToString("N0") + "개", FontSize = 12.5, Foreground = Theme.B(Theme.Fg), TextAlignment = TextAlignment.Right };
                Grid.SetColumn(files, 1);
                Grid.SetColumn(count, 2);
                row.Children.Add(name);
                row.Children.Add(files);
                row.Children.Add(count);
                previewList.Children.Add(row);
            }
            btnGo.Content = "정리하기 (" + plan.Count.ToString("N0") + "개)";
            btnGo.IsEnabled = true;
        }

        // ----- 실행 · 되돌리기
        static string UndoPath { get { return IO.Path.Combine(Config.Dir, "organize_undo.txt"); } }

        static string Unique(string path)
        {
            if (!IO.File.Exists(path)) return path;
            string dir = IO.Path.GetDirectoryName(path), name = IO.Path.GetFileNameWithoutExtension(path), ext = IO.Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                string p = IO.Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!IO.File.Exists(p)) return p;
            }
            return IO.Path.Combine(dir, name + " (" + Guid.NewGuid().ToString("N").Substring(0, 6) + ")" + ext);
        }

        void Apply()
        {
            string folder = txtFolder.Text.Trim().Trim('"');
            var moves = plan.ToList();
            btnGo.IsEnabled = false;
            previewTitle.Text = "옮기는 중…";
            string sid = scheme;
            bool old = chkOld.IsChecked == true, sub = chkSub.IsChecked == true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var done = new List<string>();
                int failed = 0;
                foreach (var m in moves)
                {
                    try
                    {
                        string dir = IO.Path.Combine(folder, m.Folder);
                        IO.Directory.CreateDirectory(dir);
                        string dest = Unique(IO.Path.Combine(dir, IO.Path.GetFileName(m.From)));
                        IO.File.Move(m.From, dest);
                        done.Add(m.From + "\t" + dest);
                    }
                    catch (Exception) { failed++; }
                }
                try
                {
                    IO.Directory.CreateDirectory(Config.Dir);
                    IO.File.WriteAllLines(UndoPath, new[] { "#" + folder + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") }.Concat(done).ToArray(), new UTF8Encoding(false));
                }
                catch (Exception) { }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var f = Config.Load();
                        f.OrganizeLastScheme = sid;
                        f.OrganizeOldVersions = old;
                        f.OrganizeSubfolders = sub;
                        f.OrganizeRecent = new[] { folder }.Concat(f.OrganizeRecent.Where(x => !string.Equals(x, folder, StringComparison.OrdinalIgnoreCase))).Take(5).ToArray();
                        f.Save();
                        app.Cfg = f;
                    }
                    catch (Exception ex) { RingApp.Log("파일 정리 설정 저장 오류: " + ex.Message); }
                    ShowDone(folder, done.Count, failed);
                }));
            });
        }

        void ShowDone(string folder, int moved, int failed)
        {
            content.Children.Clear();
            var big = new TextBlock { Text = "파일 " + moved.ToString("N0") + "개를 정리했어요", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg), Margin = new Thickness(0, 6, 0, 4) };
            content.Children.Add(big);
            content.Children.Add(Small(failed > 0 ? "열려 있거나 권한이 없는 파일 " + failed + "개는 그대로 두었어요." : "지운 파일은 없어요. 마음에 안 들면 바로 되돌릴 수 있어요."));
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var undo = new Button { Content = "되돌리기", Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 8, 0) };
            undo.Click += (s, e) => { int n = Undo(app); app.Toast(n > 0 ? "파일 " + n + "개를 원래 자리로 돌려놨어요" : "되돌릴 정리가 없어요", 2400); Close(); };
            var open = new Button { Content = "폴더 열기", Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 8, 0) };
            open.Click += (s, e) => { try { Process.Start("explorer.exe", "\"" + folder + "\""); } catch (Exception) { } Close(); };
            var close = new Button { Content = "닫기", Height = 36, Padding = new Thickness(18, 0, 18, 0), Background = Theme.B(Theme.Fg), Foreground = Theme.B(Theme.Bg) };
            close.Click += (s, e) => Close();
            row.Children.Add(undo);
            row.Children.Add(open);
            row.Children.Add(close);
            content.Children.Add(row);
        }

        public static bool CanUndo() { return IO.File.Exists(UndoPath); }

        public static string LastInfo()
        {
            try
            {
                if (!IO.File.Exists(UndoPath)) return null;
                var first = IO.File.ReadLines(UndoPath, Encoding.UTF8).FirstOrDefault() ?? "";
                var parts = first.TrimStart('#').Split('\t');
                int n = IO.File.ReadLines(UndoPath, Encoding.UTF8).Count() - 1;
                return (parts.Length > 1 ? parts[1] + " · " : "") + IO.Path.GetFileName(parts[0].TrimEnd('\\')) + " · " + n + "개";
            }
            catch (Exception) { return null; }
        }

        // 마지막 정리를 되돌림 (원래 자리에 같은 이름 파일이 생겼으면 그 파일은 그대로 둠)
        public static int Undo(RingApp app)
        {
            if (!IO.File.Exists(UndoPath)) return 0;
            int n = 0;
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in IO.File.ReadAllLines(UndoPath, Encoding.UTF8).Skip(1).Reverse())
            {
                var p = line.Split('\t');
                if (p.Length != 2) continue;
                try
                {
                    if (IO.File.Exists(p[1]) && !IO.File.Exists(p[0]))
                    {
                        IO.File.Move(p[1], p[0]);
                        dirs.Add(IO.Path.GetDirectoryName(p[1]));
                        n++;
                    }
                }
                catch (Exception) { }
            }
            foreach (var d in dirs)
            {
                try { if (IO.Directory.Exists(d) && !IO.Directory.EnumerateFileSystemEntries(d).Any()) IO.Directory.Delete(d); } catch (Exception) { }
            }
            try { IO.File.Delete(UndoPath); } catch (Exception) { }
            return n;
        }
    }

    // ------------------------------------------------------------ 설정 미리보기 (링을 축소해서 그려 줌)
    class PreviewModel
    {
        public double R = 110;
        public double Ui = 1.0;
        public int Dim = 30;
        public Color Accent = Color.FromRgb(0xD9, 0x77, 0x57);
        public string[] Slots = new string[9];
        public Func<string, string> Label;
        public Func<string, string> Icon;
        public Func<string, Color> ColorOf;
    }

    class RingPreview : Canvas
    {
        public PreviewModel Model;
        public double Scale = 0.5;
        public bool ShowDesktop = true;
        public bool ShowLabels = true;
        public int Selected = -1;
        public Action<int> SlotClicked;
        public double PlaySpeed = 1;
        DispatcherTimer timer;
        DateTime playStart;

        public RingPreview(PreviewModel model, double width, double height)
        {
            Model = model;
            Width = width;
            Height = height;
            ClipToBounds = true;
        }

        public void Refresh() { Draw(5000); }

        public void Play(double speed)
        {
            PlaySpeed = speed;
            if (timer == null)
            {
                timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (s, e) =>
                {
                    double t = (DateTime.Now - playStart).TotalMilliseconds * PlaySpeed;
                    Draw(t);
                    if (t > 1100) timer.Stop();
                };
            }
            playStart = DateTime.Now;
            timer.Start();
        }

        static double Clamp01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }
        static double EaseOutCubic(double t) { return 1 - Math.Pow(1 - t, 3); }
        static double EaseOutBack(double t)
        {
            const double c1 = 1.70158, c3 = c1 + 1;
            return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
        }

        void Add(UIElement e, double x, double y)
        {
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            Children.Add(e);
        }

        public void Draw(double t)
        {
            Children.Clear();
            var m = Model;
            double k = Scale;
            double cx = Width / 2, cy = Height / 2;
            var accentBrush = new SolidColorBrush(m.Accent);

            if (ShowDesktop)
            {
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE6, 0xDF));
                var win = new Border { Width = Width - 40, Height = Height - 36, Background = Brushes.White, CornerRadius = new CornerRadius(8) };
                var lines = new StackPanel { Margin = new Thickness(18) };
                for (int i = 0; i < 9; i++)
                    lines.Children.Add(new Border { Height = 6, Width = (i % 3 == 0 ? 0.55 : 0.8) * (Width - 80), Background = new SolidColorBrush(Color.FromRgb(0xE4, 0xE2, 0xDA)), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 0, 12), HorizontalAlignment = HorizontalAlignment.Left });
                win.Child = lines;
                Add(win, 20, 18);
                Add(new Shapes.Rectangle { Width = Width, Height = Height, Fill = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * m.Dim / 100.0), 0, 0, 0)) }, 0, 0);
            }
            else
            {
                Background = Brushes.Transparent;
            }

            double R = m.R * k;
            // 안내 원
            var guide = new Shapes.Ellipse { Width = 2 * R, Height = 2 * R, Stroke = new SolidColorBrush(Color.FromArgb(0x90, 0x8C, 0x8A, 0x83)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection(new double[] { 2, 3 }), Opacity = Clamp01((t - 200) / 300) };
            Add(guide, cx - R, cy - R);

            // 파동
            for (int kk = 0; kk < 3; kk++)
            {
                double tt = (t - kk * 110) / 720;
                if (tt < 0 || tt >= 1) continue;
                double size = (2 * m.R + 40 - 46 * kk) * k * (0.15 + 1.1 * EaseOutCubic(tt));
                Add(new Shapes.Ellipse { Width = size, Height = size, Stroke = accentBrush, StrokeThickness = Math.Max(0.6, (3 - kk) * (1 - tt) * k * 1.4), Opacity = 0.9 * (1 - tt) }, cx - size / 2, cy - size / 2);
            }

            double B = 64 * k, C = 92 * k;
            for (int i = 0; i < 9; i++)
            {
                double ang = (-90 + i * 40) * Math.PI / 180;
                string id = m.Slots[i];
                bool empty = string.IsNullOrEmpty(id);

                // 스포크
                double st = Clamp01((t - (70 + i * 16)) / 520);
                if (st > 0)
                {
                    double grow = EaseOutCubic(Math.Min(st / 0.55, 1));
                    double fade = st < 0.55 ? 0 : (st - 0.55) / 0.45;
                    double r0 = C / 2 + 2, r1 = r0 + (R - B / 2 - r0) * grow;
                    Children.Add(new Shapes.Line
                    {
                        X1 = cx + Math.Cos(ang) * r0, Y1 = cy + Math.Sin(ang) * r0,
                        X2 = cx + Math.Cos(ang) * r1, Y2 = cy + Math.Sin(ang) * r1,
                        Stroke = accentBrush, StrokeThickness = Math.Max(1, 2 * k),
                        Opacity = 1 - (empty ? 0.8 : 0.7) * fade
                    });
                }

                // 버블
                double bt = Clamp01((t - (170 + i * 40)) / 440);
                if (bt <= 0) continue;
                double s = EaseOutBack(bt) * (Selected == i ? 1.12 : 1);
                double bx = cx + Math.Cos(ang) * R, by = cy + Math.Sin(ang) * R;
                var g = new Grid { Width = B, Height = B, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(s, s) };
                var col = empty ? m.Accent : m.ColorOf(id);
                var circle = new Shapes.Ellipse();
                var icon = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                bool sel = Selected == i;
                if (empty)
                {
                    circle.Fill = new SolidColorBrush(sel ? Color.FromArgb(0x40, m.Accent.R, m.Accent.G, m.Accent.B) : Color.FromArgb(0x30, 0x90, 0x8E, 0x86));
                    circle.Stroke = sel ? accentBrush : new SolidColorBrush(Color.FromRgb(0xA8, 0xA6, 0x9E));
                    circle.StrokeThickness = Math.Max(1, 2 * k);
                    circle.StrokeDashArray = new DoubleCollection(new double[] { 3, 2 });
                    icon.Text = "+";
                    icon.FontSize = 22 * k;
                    icon.Foreground = new SolidColorBrush(Color.FromRgb(0x8C, 0x8A, 0x83));
                }
                else
                {
                    circle.Fill = new SolidColorBrush(sel ? col : Colors.White);
                    if (!ShowDesktop) { circle.Stroke = new SolidColorBrush(Theme.Line); circle.StrokeThickness = 1; }
                    icon.Text = m.Icon(id);
                    icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                    icon.FontSize = 24 * k;
                    icon.Foreground = new SolidColorBrush(sel ? Colors.White : col);
                    g.Effect = new DropShadowEffect { BlurRadius = 10 * k, ShadowDepth = 3 * k, Opacity = 0.3, Direction = 270 };
                }
                g.Children.Add(circle);
                g.Children.Add(icon);
                if (SlotClicked != null)
                {
                    int idx = i;
                    g.Cursor = Cursors.Hand;
                    g.Background = Brushes.Transparent;
                    g.MouseLeftButtonUp += (o, e) => SlotClicked(idx);
                }
                Add(g, bx - B / 2, by - B / 2);

                // 이름표
                if (ShowLabels && (!empty || SlotClicked != null) && t > 380 + i * 40)
                {
                    var lb = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8 * k + 2, 2, 8 * k + 2, 3), IsHitTestVisible = false };
                    var tb = new TextBlock { Text = empty ? (i + 1) + "번 빈 칸" : m.Label(id), FontFamily = Theme.Font, FontSize = Math.Max(8, 13 * m.Ui * k) };
                    if (ShowDesktop) { lb.Background = new SolidColorBrush(sel ? Color.FromRgb(0xFA, 0xF9, 0xF5) : Color.FromArgb(0xDD, 0x2B, 0x2A, 0x27)); tb.Foreground = new SolidColorBrush(sel ? Color.FromRgb(0x14, 0x14, 0x13) : Color.FromRgb(0xFA, 0xF9, 0xF5)); }
                    else { lb.Background = new SolidColorBrush(sel ? m.Accent : Theme.Sel); tb.Foreground = new SolidColorBrush(sel ? Colors.White : Theme.Fg); }
                    if (empty) tb.Foreground = new SolidColorBrush(sel ? Colors.White : Color.FromRgb(0x8C, 0x8A, 0x83));
                    lb.Child = tb;
                    lb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double gap = ShowDesktop ? 62 * k : 54 * k;
                    double lx = cx + Math.Cos(ang) * (R + gap), ly = cy + Math.Sin(ang) * (R + gap);
                    Add(lb, lx - lb.DesiredSize.Width / 2, ly - lb.DesiredSize.Height / 2);
                }
            }

            // 코어
            double cs = EaseOutBack(Clamp01(t / 300));
            var core = new Grid { Width = C, Height = C, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(cs, cs) };
            core.Children.Add(new Shapes.Ellipse { Fill = accentBrush, Stroke = new SolidColorBrush(Color.FromRgb(0xFA, 0xF9, 0xF5)), StrokeThickness = Math.Max(1, 3 * k) });
            var star = new Shapes.Path
            {
                Data = Geometry.Parse("M12,2.5 C12.5,7.3 15.7,10.5 20.5,11 L20.5,13 C15.7,13.5 12.5,16.7 12,21.5 L10,21.5 C9.5,16.7 6.3,13.5 1.5,13 L1.5,11 C6.3,10.5 9.5,7.3 10,2.5 Z"),
                Fill = Brushes.White, Width = 26 * k, Height = 26 * k, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            core.Children.Add(star);
            core.Effect = new DropShadowEffect { BlurRadius = 14 * k, ShadowDepth = 3 * k, Opacity = 0.35, Direction = 270 };
            Add(core, cx - C / 2, cy - C / 2);
        }
    }

    // ------------------------------------------------------------ TeamCal 스타일 테마
    static class Theme
    {
        public static bool Dark;
        public static readonly FontFamily Font = new FontFamily("IBM Plex Sans KR, Malgun Gothic");

        static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static Color Bg { get { return Dark ? C("#111113") : C("#fbfaf7"); } }
        public static Color Fg { get { return Dark ? C("#f2f2f0") : C("#1b1b1d"); } }
        public static Color Muted { get { return Dark ? C("#a0a0a7") : C("#6b6b70"); } }
        public static Color Line { get { return Dark ? C("#26262b") : C("#ebe8e1"); } }
        public static Color Sel { get { return Dark ? C("#1f1f24") : C("#f0ede6"); } }
        public static Color Field { get { return Dark ? C("#18181c") : C("#ffffff"); } }
        public static Color Dot { get { return Dark ? C("#3a3a41") : C("#cfcbc2"); } }
        public static Color Err { get { return Dark ? C("#ff7a6b") : C("#c23b2f"); } }

        public static SolidColorBrush B(Color c) { return new SolidColorBrush(c); }

        public static void Load(Config c) { Dark = c.Theme == "dark"; }

        static string H(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        // 창 하나에 TeamCal 모양의 기본 컨트롤 스타일을 입힘 (색은 DynamicResource라 바로 바꿀 수 있음)
        public static ResourceDictionary Styles(Color accent)
        {
            string xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='{DynamicResource FgBrush}'/>
    <Setter Property='CaretBrush' Value='{DynamicResource FgBrush}'/>
    <Setter Property='Padding' Value='8,5'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='TextBox'>
        <Border x:Name='b' CornerRadius='8' BorderThickness='1' BorderBrush='{DynamicResource LineBrush}' Background='{DynamicResource FieldBrush}' Padding='{TemplateBinding Padding}'>
          <ScrollViewer x:Name='PART_ContentHost' Margin='0'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource AccentBrush}'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='Button'>
    <Setter Property='Foreground' Value='{DynamicResource FgBrush}'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Padding' Value='11,5'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='Button'>
        <Border x:Name='b' CornerRadius='8' BorderThickness='1' BorderBrush='{DynamicResource LineBrush}' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource DotBrush}'/></Trigger>
          <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.8'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{DynamicResource FgBrush}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='CheckBox'>
        <StackPanel Orientation='Horizontal' Background='Transparent'>
          <Border x:Name='track' Width='34' Height='20' CornerRadius='10' Background='{DynamicResource DotBrush}' VerticalAlignment='Center'>
            <Ellipse x:Name='knob' Width='16' Height='16' Fill='White' HorizontalAlignment='Left' Margin='2,0'/>
          </Border>
          <ContentPresenter x:Name='cp' Margin='10,0,0,0' VerticalAlignment='Center'/>
        </StackPanel>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='track' Property='Background' Value='{DynamicResource AccentBrush}'/>
            <Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/>
          </Trigger>
          <Trigger Property='Content' Value='{x:Null}'><Setter TargetName='cp' Property='Margin' Value='0'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='RadioButton'>
    <Setter Property='Foreground' Value='{DynamicResource FgBrush}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='RadioButton'>
        <StackPanel Orientation='Horizontal' Background='Transparent'>
          <Grid Width='16' Height='16' VerticalAlignment='Center'>
            <Ellipse x:Name='ring' Stroke='{DynamicResource DotBrush}' StrokeThickness='1.5' Fill='{DynamicResource FieldBrush}'/>
            <Ellipse x:Name='dot' Width='8' Height='8' Fill='{DynamicResource AccentBrush}' Visibility='Collapsed'/>
          </Grid>
          <ContentPresenter Margin='8,0,0,0' VerticalAlignment='Center'/>
        </StackPanel>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='dot' Property='Visibility' Value='Visible'/>
            <Setter TargetName='ring' Property='Stroke' Value='{DynamicResource AccentBrush}'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style x:Key='SliderPart' TargetType='RepeatButton'>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='RepeatButton'>
        <Border Background='Transparent'><Border Height='4' CornerRadius='2' Background='{TemplateBinding Background}' VerticalAlignment='Center'/></Border>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='Slider'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='Slider'>
        <Grid Height='22' Background='Transparent'>
          <Track x:Name='PART_Track'>
            <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource SliderPart}' Background='{DynamicResource AccentBrush}' Command='Slider.DecreaseLarge'/></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource SliderPart}' Background='{DynamicResource DotBrush}' Command='Slider.IncreaseLarge'/></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'>
              <Ellipse Width='16' Height='16' Fill='White' Stroke='{DynamicResource AccentBrush}' StrokeThickness='2'/>
            </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='GroupBox'>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='GroupBox'>
        <StackPanel>
          <ContentPresenter ContentSource='Header' TextElement.FontSize='11' TextElement.FontWeight='Bold' TextElement.Foreground='{DynamicResource MutedBrush}' Margin='0,14,0,6'/>
          <Border Background='{DynamicResource FieldBrush}' BorderBrush='{DynamicResource LineBrush}' BorderThickness='1' CornerRadius='12' Padding='14,12'>
            <ContentPresenter/>
          </Border>
        </StackPanel>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='ListBox'>
    <Setter Property='Foreground' Value='{DynamicResource FgBrush}'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='ListBox'>
        <Border CornerRadius='10' BorderThickness='1' BorderBrush='{DynamicResource LineBrush}' Background='{DynamicResource BgBrush}' Padding='4'>
          <ScrollViewer Focusable='False'><ItemsPresenter/></ScrollViewer>
        </Border>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='ListBoxItem'>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='ListBoxItem'>
        <Border x:Name='b' CornerRadius='8' Padding='8,4' Background='Transparent'><ContentPresenter/></Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource SelBrush}'/></Trigger>
          <Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource SelBrush}'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='TextBlock'>
    <Setter Property='TextOptions.TextFormattingMode' Value='Display'/>
  </Style>
</ResourceDictionary>";
            var dict = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
            SetColors(dict, accent);
            return dict;
        }

        public static void SetColors(ResourceDictionary d, Color accent)
        {
            d["FgBrush"] = B(Fg);
            d["MutedBrush"] = B(Muted);
            d["LineBrush"] = B(Line);
            d["SelBrush"] = B(Sel);
            d["FieldBrush"] = B(Field);
            d["DotBrush"] = B(Dot);
            d["BgBrush"] = B(Bg);
            d["AccentBrush"] = B(accent);
        }
    }

    // ------------------------------------------------------------ 전체 설정 창 (TeamCal 스타일 · 바로 적용)
    class GeneralSettingsWindow : Window
    {
        public static readonly string VERSION = "v" + RingApp.APP_VERSION;
        static GeneralSettingsWindow instance;
        static readonly string[][] ACCENTS = {
            new[] { "#e0592a", "오렌지 (TeamCal)" }, new[] { "#D97757", "클레이" }, new[] { "#2f6fdb", "블루" },
            new[] { "#178a6b", "그린" }, new[] { "#8e5bd6", "퍼플" }, new[] { "#d2a01b", "골드" }, new[] { "#c23b4a", "레드" }
        };
        static readonly string[][] FILTERS = { new[] { "0", "전체" }, new[] { "1", "CAD·3D" }, new[] { "2", "PDF" }, new[] { "3", "문서" }, new[] { "4", "이미지" }, new[] { "5", "폴더" } };
        const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        RingApp app;
        Config cfg;                 // 지금 편집 중인 값 (바뀌면 곧바로 저장)
        PreviewModel model = new PreviewModel();
        ResourceDictionary styles;
        Border card;
        StackPanel tabBar;
        List<Border> tabButtons = new List<Border>();
        ScrollViewer body;
        List<Func<UIElement>> tabBuilders = new List<Func<UIElement>>();
        List<string> tabNames = new List<string>();
        int currentTab;
        bool manualPos;
        DispatcherTimer saveTimer;
        RingPreview screenPreview, slotPreview;
        int selSlot;
        Action refreshSlotPanel;
        string savedHotkey;

        public static void Open(RingApp app) { Open(app, 0); }

        public static void OpenAt(RingApp app, string section)
        {
            int tab = 0;
            int slot = -1;
            if (section == "ring") tab = 1;
            else if (section == "slots") tab = 2;
            else if (section != null && section.StartsWith("slot:"))
            {
                tab = 2;
                int.TryParse(section.Substring(5), out slot);
            }
            else if (section != null)
            {
                int k = app.Actions.Keys.ToList().IndexOf(section);
                if (k >= 0) tab = 3 + k;
            }
            if (slot >= 0 && slot < 9)
            {
                pendingSlot = slot;
                if (instance != null) instance.selSlot = slot;
            }
            Open(app, tab);
        }

        static int pendingSlot = -1;

        static void Open(RingApp app, int tab)
        {
            if (instance != null) { instance.Activate(); instance.ShowTab(tab); return; }
            var w = new GeneralSettingsWindow(app, tab);
            instance = w;
            w.Closed += (s, e) => { if (instance == w) instance = null; };
            w.Show();
            w.Activate();
        }

        GeneralSettingsWindow(RingApp owner, int tab)
        {
            app = owner;
            cfg = Config.Load();
            savedHotkey = cfg.Hotkey;
            Theme.Load(cfg);
            if (pendingSlot >= 0) { selSlot = pendingSlot; pendingSlot = -1; }

            Title = "설정 - Claude 액션링";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            FontFamily = Theme.Font;
            FontSize = 13;
            ShowInTaskbar = true;
            Loaded += (s, e) =>
            {
                if (manualPos) return;
                var tl = app.PlaceInMonitor(app.CursorDip(), ActualWidth, ActualHeight);
                Left = tl.X; Top = tl.Y;
            };
            styles = Theme.Styles(Accent());
            Resources = styles;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            model.Label = id => app.LabelFor(id, cfg);
            model.Icon = id => { ActionDef a; return app.Actions.TryGetValue(id, out a) ? a.Icon : "?"; };
            model.ColorOf = id => app.ColorFor(id, cfg);

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); SaveNow(); };
            Closed += (s, e) => { if (saveTimer.IsEnabled) { saveTimer.Stop(); SaveNow(); } };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox)) Close(); };

            tabNames.Add("일반"); tabBuilders.Add(GeneralTab);
            tabNames.Add("링"); tabBuilders.Add(RingTab);
            tabNames.Add("버블 배치"); tabBuilders.Add(SlotsTab);
            foreach (var a in app.Actions.Values)
            {
                ActionDef act = a;
                tabNames.Add(a.Label);
                tabBuilders.Add(() => BubbleTab(act));
            }

            BuildFrame();
            ShowTab(tab);
        }

        Color Accent()
        {
            try { return (Color)ColorConverter.ConvertFromString(cfg.Accent); }
            catch (Exception) { return Color.FromRgb(0xe0, 0x59, 0x2a); }
        }

        // ----- 틀: 둥근 시트 + 제목 + 밑줄 탭 + 아래 안내
        void BuildFrame()
        {
            card = new Border
            {
                Width = 720,
                Margin = new Thickness(18),
                CornerRadius = new CornerRadius(20),
                Background = Theme.B(Theme.Bg),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 36, ShadowDepth = 10, Opacity = 0.22, Direction = 270 }
            };
            card.LayoutTransform = new ScaleTransform(app.UiScaleOf(cfg), app.UiScaleOf(cfg));
            var dock = new DockPanel();
            card.Child = dock;
            Content = card;

            // 제목 줄 (끌어서 옮기기)
            var head = new DockPanel { Margin = new Thickness(18, 16, 14, 0), Background = Brushes.Transparent };
            head.MouseLeftButtonDown += (s, e) => { if (!e.Handled && e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch (Exception) { } } };
            var close = IconButton("", "닫기");
            close.MouseLeftButtonDown += (s, e) => { e.Handled = true; Close(); };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            var titles = new StackPanel { Orientation = Orientation.Horizontal };
            titles.Children.Add(new TextBlock { Text = "설정", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg), VerticalAlignment = VerticalAlignment.Center });
            titles.Children.Add(new TextBlock { Text = "Claude 액션링", FontSize = 12, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(8, 3, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(titles);
            DockPanel.SetDock(head, Dock.Top);
            dock.Children.Add(head);

            // 탭
            var tabsWrap = new Border { BorderBrush = Theme.B(Theme.Line), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(10, 0, 10, 0) };
            tabBar = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < tabNames.Count; i++)
            {
                int idx = i;
                var tb = new Border { Padding = new Thickness(9, 7, 9, 9), BorderThickness = new Thickness(0, 0, 0, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent };
                tb.Child = new TextBlock { Text = tabNames[i], FontSize = 12.5, FontWeight = FontWeights.SemiBold };
                tb.MouseLeftButtonUp += (s, e) => ShowTab(idx);
                tabButtons.Add(tb);
                tabBar.Children.Add(tb);
            }
            tabsWrap.Child = new ScrollViewer { Content = tabBar, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            DockPanel.SetDock(tabsWrap, Dock.Top);
            dock.Children.Add(tabsWrap);

            // 아래 안내
            var foot = new DockPanel { Margin = new Thickness(0) };
            var footBorder = new Border { BorderBrush = Theme.B(Theme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 10, 18, 12), Child = foot };
            var ver = new TextBlock { Text = VERSION, FontSize = 11, Foreground = Theme.B(Theme.Muted) };
            DockPanel.SetDock(ver, Dock.Right);
            foot.Children.Add(ver);
            foot.Children.Add(new TextBlock { Text = "바뀐 내용은 바로 적용돼요", FontSize = 11, Foreground = Theme.B(Theme.Muted) });
            DockPanel.SetDock(footBorder, Dock.Bottom);
            dock.Children.Add(footBorder);

            body = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(18, 4, 18, 16) };
            body.MaxHeight = Math.Max(360, app.WorkAreaAt(app.CursorDip()).Height / app.UiScaleOf(cfg) - 220);
            body.Height = Math.Min(560, body.MaxHeight);
            dock.Children.Add(body);
        }

        Border IconButton(string glyph, string tip)
        {
            var b = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tip };
            b.Child = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11, Foreground = Theme.B(Theme.Muted), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            b.MouseEnter += (s, e) => b.Background = Theme.B(Theme.Sel);
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            return b;
        }

        void ShowTab(int i)
        {
            if (i < 0 || i >= tabBuilders.Count) i = 0;
            currentTab = i;
            for (int k = 0; k < tabButtons.Count; k++)
            {
                bool on = k == i;
                tabButtons[k].BorderBrush = on ? Theme.B(Theme.Fg) : Brushes.Transparent;
                ((TextBlock)tabButtons[k].Child).Foreground = Theme.B(on ? Theme.Fg : Theme.Muted);
            }
            screenPreview = null;
            slotPreview = null;
            body.Content = tabBuilders[i]();
            body.ScrollToTop();
        }

        // ----- TeamCal 모양 부품
        static TextBlock Grp(string t)
        {
            return new TextBlock { Text = t, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 16, 0, 2) };
        }

        static Border SRow(string label, string sub, UIElement right)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            st.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = Theme.B(Theme.Fg), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(sub)) st.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap });
            g.Children.Add(st);
            if (right != null)
            {
                var fe = right as FrameworkElement;
                if (fe != null) fe.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 1);
                g.Children.Add(right);
            }
            return new Border { Child = g, Padding = new Thickness(0, 9, 0, 9), BorderBrush = Theme.B(Theme.Line), BorderThickness = new Thickness(0, 0, 0, 1) };
        }

        // 세그먼트 버튼 (TeamCal .sseg)
        static Border Seg(string[][] opts, string value, Action<string> pick)
        {
            var wrap = new Border { Background = Theme.B(Theme.Sel), CornerRadius = new CornerRadius(9), Padding = new Thickness(2) };
            var sp = new WrapPanel();
            var items = new List<Border>();
            Action<string> paint = v =>
            {
                foreach (var it in items)
                {
                    bool on = (string)it.Tag == v;
                    it.Background = on ? Theme.B(Theme.Bg) : Brushes.Transparent;
                    it.Effect = on ? new DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Opacity = 0.12, Direction = 270 } : null;
                    ((TextBlock)it.Child).Foreground = Theme.B(on ? Theme.Fg : Theme.Muted);
                }
            };
            foreach (var o in opts)
            {
                string v = o[0];
                var b = new Border { Padding = new Thickness(10, 4, 10, 5), CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand, Tag = v };
                b.Child = new TextBlock { Text = o[1], FontSize = 11.5, FontWeight = FontWeights.SemiBold };
                b.MouseLeftButtonUp += (s, e) => { paint(v); pick(v); };
                items.Add(b);
                sp.Children.Add(b);
            }
            wrap.Child = sp;
            paint(value);
            return wrap;
        }

        // 슬라이더 + 값 (TeamCal .srange)
        static Grid Range(double min, double max, double step, double value, string unit, Action<double> changed)
        {
            var g = new Grid { Width = 190 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            var sl = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = value, VerticalAlignment = VerticalAlignment.Center };
            var em = new TextBlock { Text = Math.Round(value) + unit, FontSize = 11.5, Foreground = Theme.B(Theme.Muted), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            sl.ValueChanged += (s, e) => { em.Text = Math.Round(sl.Value) + unit; changed(sl.Value); };
            Grid.SetColumn(em, 1);
            g.Children.Add(sl);
            g.Children.Add(em);
            return g;
        }

        static CheckBox Switch(bool on, Action<bool> changed)
        {
            var c = new CheckBox { IsChecked = on };
            c.Checked += (s, e) => changed(true);
            c.Unchecked += (s, e) => changed(false);
            return c;
        }

        static Button SBtn(string text, bool primary, bool danger)
        {
            var b = new Button { Content = text, Height = 28, Padding = new Thickness(11, 0, 11, 0) };
            if (primary) { b.Background = Theme.B(Theme.Fg); b.Foreground = Theme.B(Theme.Bg); }
            if (danger) b.Foreground = Theme.B(Theme.Err);
            return b;
        }

        WrapPanel Swatches(string value, bool allowFollow, Action<string> pick)
        {
            var wrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var list = new List<Border>();
            Action<string> mark = v =>
            {
                foreach (var sw in list)
                    sw.BorderThickness = new Thickness(string.Equals((string)sw.Tag, v ?? "", StringComparison.OrdinalIgnoreCase) ? 2.5 : 0);
            };
            var all = new List<string[]>();
            if (allowFollow) all.Add(new[] { "", "강조 색 따라가기" });
            all.AddRange(ACCENTS);
            foreach (var a in all)
            {
                string hex = a[0];
                var sw = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Hand, ToolTip = a[1], Tag = hex, BorderBrush = Theme.B(Theme.Fg) };
                if (hex.Length == 0)
                {
                    sw.Background = Theme.B(Theme.Sel);
                    sw.Child = new TextBlock { Text = "A", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Muted), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
                else sw.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                sw.MouseLeftButtonUp += (s, e) => { mark(hex); pick(hex); };
                list.Add(sw);
                wrap.Children.Add(sw);
            }
            mark(value);
            return wrap;
        }

        // ----- 저장 (바로 적용)
        void Changed() { saveTimer.Stop(); saveTimer.Start(); }

        void SaveNow()
        {
            try
            {
                var f = Config.Load(); // 검색 폴더 등은 그 창에서 따로 저장하므로 지금 파일 기준으로 덮어씀
                f.Theme = cfg.Theme;
                f.UiScale = cfg.UiScale;
                f.Accent = cfg.Accent;
                f.Radius = cfg.Radius;
                f.DimPercent = cfg.DimPercent;
                f.Animation = cfg.Animation;
                f.Slots = (string[])cfg.Slots.Clone();
                f.BubbleLabels = new Dictionary<string, string>(cfg.BubbleLabels);
                f.BubbleColors = new Dictionary<string, string>(cfg.BubbleColors);
                f.SearchWindowPos = cfg.SearchWindowPos;
                f.SearchKeepQuery = cfg.SearchKeepQuery;
                f.SearchDefaultFilter = cfg.SearchDefaultFilter;
                f.OrganizeOldVersions = cfg.OrganizeOldVersions;
                f.OrganizeSubfolders = cfg.OrganizeSubfolders;
                f.CaptureFolder = cfg.CaptureFolder;
                f.CaptureAutoSave = cfg.CaptureAutoSave;
                f.CaptureFormat = cfg.CaptureFormat;
                f.AskTarget = cfg.AskTarget;
                f.AskIncludeClipboard = cfg.AskIncludeClipboard;
                f.MemoTarget = cfg.MemoTarget;
                f.MemoNewNote = cfg.MemoNewNote;
                f.UpdateUrl = cfg.UpdateUrl;
                f.WindowMonitor = cfg.WindowMonitor;
                f.WindowCorner = cfg.WindowCorner;
                f.ClaudeAppPath = cfg.ClaudeAppPath;
                f.Hotkey = cfg.Hotkey;
                f.Save();
                app.Cfg = f;
                if (!string.Equals(savedHotkey, f.Hotkey, StringComparison.OrdinalIgnoreCase))
                {
                    if (app.ApplyHotkey(f.Hotkey)) { savedHotkey = f.Hotkey; app.Toast("단축키를 " + f.Hotkey + "(으)로 바꿨어요", 1800); }
                    else app.Toast("단축키(" + f.Hotkey + ")를 등록하지 못했어요", 3000);
                }
            }
            catch (Exception ex)
            {
                RingApp.Log("설정 저장 오류: " + ex);
                app.Toast("설정을 저장하지 못했어요: " + ex.Message, 3000);
            }
        }

        // 테마/글자 크기처럼 창 모양 자체가 바뀌는 설정은 창을 새로 그림
        void Rebuild()
        {
            SaveNow();
            double left = Left, top = Top;
            int tab = currentTab;
            var next = new GeneralSettingsWindow(app, tab);
            next.WindowStartupLocation = WindowStartupLocation.Manual;
            next.manualPos = true;
            next.Left = left;
            next.Top = top;
            var old = this;
            instance = next;
            next.Closed += (s, e) => { if (instance == next) instance = null; };
            next.Show();
            old.saveTimer.Stop();
            old.Close();
        }

        void UpdatePreviews()
        {
            model.R = cfg.Radius;
            model.Ui = app.UiScaleOf(cfg);
            model.Dim = cfg.DimPercent;
            model.Accent = Accent();
            model.Slots = (string[])cfg.Slots.Clone();
            if (screenPreview != null) screenPreview.Refresh();
            if (slotPreview != null) { slotPreview.Selected = selSlot; slotPreview.Refresh(); }
        }

        // ----- 탭: 일반
        UIElement GeneralTab()
        {
            var sp = new StackPanel();
            sp.Children.Add(Grp("화면"));
            sp.Children.Add(SRow("테마", null, Seg(new[] { new[] { "light", "라이트" }, new[] { "dark", "다크" } }, cfg.Theme == "dark" ? "dark" : "light",
                v => { cfg.Theme = v; Rebuild(); })));
            string sz = cfg.UiScale < 0.95 ? "0.9" : cfg.UiScale < 1.07 ? "1" : cfg.UiScale < 1.22 ? "1.15" : "1.3";
            sp.Children.Add(SRow("글자 크기", "검색 창 · 설정 창 · 알림 · 링 이름표에 모두 적용돼요",
                Seg(new[] { new[] { "0.9", "작게" }, new[] { "1", "보통" }, new[] { "1.15", "크게" }, new[] { "1.3", "아주 크게" } }, sz,
                v => { cfg.UiScale = double.Parse(v, CultureInfo.InvariantCulture); Rebuild(); })));
            sp.Children.Add(SRow("강조 색", "링 · 스위치 · 강조 표시에 쓰여요", Swatches(cfg.Accent, false, hex =>
            {
                cfg.Accent = hex;
                Theme.SetColors(styles, Accent());
                UpdatePreviews();
                Changed();
            })));

            sp.Children.Add(Grp("업데이트"));
            if (app.UpdateAvailable)
            {
                var goBtn = SBtn("지금 업데이트", true, false);
                goBtn.Click += (s, e) => Updater.PromptAndApply(app);
                sp.Children.Add(SRow("새 버전 v" + app.UpdateVersion + " 이 있어요", string.IsNullOrEmpty(app.UpdateNotes) ? "눌러서 업데이트하세요" : app.UpdateNotes, goBtn));
            }
            var upBtn = SBtn("새 버전 확인", false, false);
            upBtn.Click += (s, e) => Updater.Check(app, true);
            sp.Children.Add(SRow("현재 버전", "v" + RingApp.APP_VERSION + " · 켤 때와 30분마다 자동 확인", upBtn));
            var uu = new TextBox { Text = cfg.UpdateUrl, Width = 300 };
            Action applyUu = () => { cfg.UpdateUrl = uu.Text.Trim(); Changed(); };
            uu.LostFocus += (s, e) => applyUu();
            uu.KeyDown += (s, e) => { if (e.Key == Key.Enter) applyUu(); };
            sp.Children.Add(SRow("업데이트 주소", "비우면 기본 주소 사용 (none-HongCheol/Claudering)", uu));

            sp.Children.Add(Grp("단축키"));
            var hk = new TextBox { Text = cfg.Hotkey, Width = 190 };
            Action applyHk = () =>
            {
                uint m, v;
                string t = hk.Text.Trim();
                if (t == cfg.Hotkey) return;
                if (!Native.ParseHotkey(t, out m, out v)) { app.Toast("단축키 형식이 맞지 않아요 (예: ctrl+alt+shift+f12)", 2600); hk.Text = cfg.Hotkey; return; }
                cfg.Hotkey = t;
                Changed();
            };
            hk.LostFocus += (s, e) => applyHk();
            hk.KeyDown += (s, e) => { if (e.Key == Key.Enter) applyHk(); };
            sp.Children.Add(SRow("링 여는 단축키", "Logi Options+에서 휠 클릭에 같은 단축키를 지정하세요", hk));

            sp.Children.Add(Grp("시작"));
            sp.Children.Add(SRow("Windows 시작 시 자동 실행", null, Switch(IsAutoStart(), on => SetAutoStart(on))));

            sp.Children.Add(Grp("Claude"));
            var pathText = string.IsNullOrEmpty(cfg.ClaudeAppPath) ? "자동으로 찾아요 (없으면 claude.ai)" : cfg.ClaudeAppPath;
            var browse = SBtn("찾아보기…", false, false);
            var autoBtn = SBtn("자동", false, false);
            var row = SRow("링 가운데를 누르면 여는 Claude", pathText, null);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(browse);
            autoBtn.Margin = new Thickness(6, 0, 0, 0);
            btns.Children.Add(autoBtn);
            ((Grid)row.Child).Children.Add(btns);
            Grid.SetColumn(btns, 1);
            browse.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "프로그램 (*.exe)|*.exe" };
                if (dlg.ShowDialog(this) == true) { cfg.ClaudeAppPath = dlg.FileName; Changed(); ShowTab(currentTab); }
            };
            autoBtn.Click += (s, e) => { cfg.ClaudeAppPath = ""; Changed(); ShowTab(currentTab); };
            sp.Children.Add(row);

            sp.Children.Add(Grp("기타"));
            var etc = new WrapPanel { Margin = new Thickness(0, 8, 0, 2) };
            var b1 = SBtn("설정 폴더 열기", false, false);
            b1.Click += (s, e) => { try { IO.Directory.CreateDirectory(Config.Dir); Process.Start("explorer.exe", "\"" + Config.Dir + "\""); } catch (Exception) { } };
            var b2 = SBtn("오류 기록 보기", false, false);
            b2.Click += (s, e) => { try { Process.Start("explorer.exe", "\"" + AppDomain.CurrentDomain.BaseDirectory + "\""); } catch (Exception) { } };
            var b3 = SBtn("액션링 종료", false, true);
            b3.Click += (s, e) => { Close(); app.Quit(); };
            foreach (var b in new[] { b1, b2, b3 }) { b.Margin = new Thickness(0, 0, 6, 6); etc.Children.Add(b); }
            sp.Children.Add(etc);
            return sp;
        }

        // ----- 탭: 링 (미리보기)
        UIElement RingTab()
        {
            var sp = new StackPanel();
            sp.Children.Add(Grp("미리보기 · 실제 크기의 50%"));
            screenPreview = new RingPreview(model, 440, 300) { Scale = 0.5 };
            var frame = new Border { Child = screenPreview, CornerRadius = new CornerRadius(14), BorderBrush = Theme.B(Theme.Line), BorderThickness = new Thickness(1), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 4) };
            var info = new TextBlock { FontSize = 11, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 0, 0, 4) };
            Action refreshInfo = () => info.Text = "링 전체 지름 약 " + Math.Round(2 * (cfg.Radius + 32 + 62) + 40) + "px (이름표 포함)";

            var previewRow = new Grid();
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewRow.Children.Add(frame);
            var side = new StackPanel { Margin = new Thickness(16, 10, 0, 0) };
            Grid.SetColumn(side, 1);
            var play = SBtn("▶ 효과 다시 보기", false, false);
            play.Margin = new Thickness(0, 0, 0, 8);
            play.HorizontalAlignment = HorizontalAlignment.Left;
            play.Click += (s, e) => PlayPreview();
            var real = SBtn("실제 화면에서 보기", true, false);
            real.HorizontalAlignment = HorizontalAlignment.Left;
            real.Click += (s, e) => { SaveNow(); app.ShowRingCenter(); };
            side.Children.Add(play);
            side.Children.Add(real);
            var tip = new TextBlock { Text = "값을 바꾸면 왼쪽 미리보기가 바로 바뀌어요.", FontSize = 11, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
            side.Children.Add(tip);
            previewRow.Children.Add(side);
            sp.Children.Add(previewRow);
            sp.Children.Add(info);

            sp.Children.Add(Grp("링"));
            sp.Children.Add(SRow("링 크기", "가운데에서 버블까지 거리", Range(90, 200, 5, cfg.Radius, "px", v => { cfg.Radius = v; refreshInfo(); UpdatePreviews(); Changed(); })));
            sp.Children.Add(SRow("배경 어둡게", "링이 열릴 때 화면을 얼마나 어둡게 할지", Range(0, 60, 5, cfg.DimPercent, "%", v => { cfg.DimPercent = (int)v; UpdatePreviews(); Changed(); })));
            sp.Children.Add(SRow("터지는 효과", null, Seg(new[] { new[] { "normal", "보통" }, new[] { "fast", "빠르게" }, new[] { "off", "끄기" } }, cfg.Animation,
                v => { cfg.Animation = v; Changed(); PlayPreview(); })));

            refreshInfo();
            UpdatePreviews();
            Dispatcher.BeginInvoke(new Action(PlayPreview), DispatcherPriority.Loaded);
            return sp;
        }

        void PlayPreview()
        {
            if (screenPreview == null) return;
            if (cfg.Animation == "off") screenPreview.Refresh();
            else screenPreview.Play(cfg.Animation == "fast" ? 1.7 : 1);
        }

        // ----- 탭: 버블 배치
        UIElement SlotsTab()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            slotPreview = new RingPreview(model, 400, 400) { Scale = 0.72, ShowDesktop = false };
            slotPreview.SlotClicked = i => { selSlot = i; UpdatePreviews(); refreshSlotPanel(); };
            slotPreview.Margin = new Thickness(-10, 4, 0, 0);
            grid.Children.Add(slotPreview);

            var right = new StackPanel { Margin = new Thickness(10, 16, 0, 0) };
            Grid.SetColumn(right, 1);
            var title = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg) };
            var sub = new TextBlock { FontSize = 11, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 2, 0, 12), TextWrapping = TextWrapping.Wrap, Text = "왼쪽 링에서 칸을 누르고, 넣을 기능을 고르세요" };
            right.Children.Add(title);
            right.Children.Add(sub);
            right.Children.Add(Grp("이 칸에 넣을 기능"));
            var choices = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            right.Children.Add(choices);

            var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            var prev = SBtn("◀ 이전 칸", false, false);
            var next = SBtn("다음 칸 ▶", false, false);
            prev.Margin = new Thickness(0, 0, 6, 0);
            prev.Click += (s, e) => { selSlot = (selSlot + 8) % 9; UpdatePreviews(); refreshSlotPanel(); };
            next.Click += (s, e) => { selSlot = (selSlot + 1) % 9; UpdatePreviews(); refreshSlotPanel(); };
            nav.Children.Add(prev);
            nav.Children.Add(next);
            right.Children.Add(nav);
            var tip = new TextBlock { Text = "같은 기능을 여러 칸에 넣어도 돼요. 12시와 오른쪽 칸이 손이 가장 편해요. 이름과 색은 각 버블 탭에서 바꿀 수 있어요.", FontSize = 11, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) };
            right.Children.Add(tip);
            grid.Children.Add(right);

            refreshSlotPanel = () =>
            {
                title.Text = (selSlot + 1) + "번 칸" + (selSlot == 0 ? " · 12시 방향" : "");
                choices.Children.Clear();
                var opts = new List<string[]> { new[] { "", "빈 칸" } };
                foreach (var a in app.Actions.Values) opts.Add(new[] { a.Id, app.LabelFor(a.Id, cfg) + (a.Handler == null ? " · 준비 중" : "") });
                string cur = cfg.Slots[selSlot] ?? "";
                foreach (var o in opts)
                {
                    string id = o[0];
                    bool on = id == cur;
                    var chip = new Border { Height = 30, Padding = new Thickness(11, 0, 11, 0), CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand };
                    chip.BorderBrush = on ? Theme.B(Accent()) : Theme.B(Theme.Line);
                    chip.Background = on ? Theme.B(Accent()) : Brushes.Transparent;
                    chip.Child = new TextBlock { Text = o[1], FontSize = 12, FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal, Foreground = on ? Brushes.White : Theme.B(Theme.Fg), VerticalAlignment = VerticalAlignment.Center };
                    chip.MouseLeftButtonUp += (s, e) =>
                    {
                        cfg.Slots[selSlot] = id.Length == 0 ? null : id;
                        UpdatePreviews();
                        refreshSlotPanel();
                        Changed();
                    };
                    choices.Children.Add(chip);
                }
            };
            refreshSlotPanel();
            UpdatePreviews();
            return grid;
        }

        // ----- 탭: 버블별
        UIElement BubbleTab(ActionDef a)
        {
            string id = a.Id;
            var sp = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var badge = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = Theme.B(Theme.Field), BorderBrush = Theme.B(Theme.Line), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0) };
            var icon = new TextBlock { Text = a.Icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 18, Foreground = Theme.B(app.ColorFor(id, cfg)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            badge.Child = icon;
            head.Children.Add(badge);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = app.LabelFor(id, cfg), FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Theme.B(Theme.Fg) });
            titles.Children.Add(new TextBlock { Text = a.Handler == null ? (a.Pending ?? "아직 만드는 중인 기능이에요 · 링에서 누르면 안내만 떠요") : "사용할 수 있어요", FontSize = 11, Foreground = Theme.B(Theme.Muted) });
            head.Children.Add(titles);
            sp.Children.Add(head);

            int where = Array.IndexOf(cfg.Slots, id);
            sp.Children.Add(Grp("버블"));
            string curName;
            cfg.BubbleLabels.TryGetValue(id, out curName);
            var name = new TextBox { Text = curName ?? "", Width = 190, ToolTip = "비워 두면 기본 이름(" + a.Label + ")" };
            name.TextChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text)) cfg.BubbleLabels.Remove(id); else cfg.BubbleLabels[id] = name.Text.Trim();
                Changed();
            };
            sp.Children.Add(SRow("표시 이름", "비워 두면 '" + a.Label + "'", name));
            string curColor;
            cfg.BubbleColors.TryGetValue(id, out curColor);
            sp.Children.Add(SRow("버블 색", "A = 강조 색 따라가기", Swatches(curColor ?? "", true, hex =>
            {
                if (hex.Length == 0) cfg.BubbleColors.Remove(id); else cfg.BubbleColors[id] = hex;
                icon.Foreground = Theme.B(app.ColorFor(id, cfg));
                Changed();
            })));
            var placeBtn = SBtn(where >= 0 ? (where + 1) + "번 칸 · 바꾸기" : "링에 넣기", false, false);
            placeBtn.Click += (s, e) =>
            {
                if (where < 0)
                {
                    int empty = Array.IndexOf(cfg.Slots, null);
                    if (empty >= 0) { cfg.Slots[empty] = id; selSlot = empty; Changed(); }
                }
                else selSlot = where;
                ShowTab(2);
            };
            sp.Children.Add(SRow("링 위치", where >= 0 ? "12시 방향부터 시계 방향으로 " + (where + 1) + "번째" : "지금은 링에 없어요", placeBtn));

            if (id == "memo")
            {
                sp.Children.Add(Grp("메모"));
                sp.Children.Add(new TextBlock { Text = "메모 버블을 누르면 선택한 메모 앱이 열려요.", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
                sp.Children.Add(SRow("열 메모 앱", "스티커 메모가 없으면 자동으로 메모장이 열려요", Seg(new[] { new[] { "sticky", "스티커 메모" }, new[] { "notepad", "메모장" } }, cfg.MemoTarget == "notepad" ? "notepad" : "sticky", v => { cfg.MemoTarget = v; Changed(); })));
                sp.Children.Add(SRow("새 메모로 열기", "스티커 메모를 열 때 빈 메모를 하나 새로 만들어요", Switch(cfg.MemoNewNote, on => { cfg.MemoNewNote = on; Changed(); })));
                var monOpts = new System.Collections.Generic.List<string[]> { new[] { "cursor", "마우스 따라" } };
                int scN = app.ScreenCount;
                for (int i = 1; i <= scN; i++) monOpts.Add(new[] { i.ToString(), i + " 모니터" });
                sp.Children.Add(SRow("표시 모니터", scN + "개 감지됨 · 메모 창이 뜰 모니터", Seg(monOpts.ToArray(), cfg.WindowMonitor ?? "cursor", v => { cfg.WindowMonitor = v; Changed(); })));
                var cornerOpts = new[] { new[] { "cursor", "마우스 근처" }, new[] { "center", "가운데" }, new[] { "topleft", "↖ 왼쪽 위" }, new[] { "topright", "↗ 오른쪽 위" }, new[] { "bottomleft", "↙ 왼쪽 아래" }, new[] { "bottomright", "↘ 오른쪽 아래" } };
                sp.Children.Add(SRow("정렬 위치", "그 모니터 안 어느 모서리에 메모 창을 둘지", Seg(cornerOpts, cfg.WindowCorner ?? "cursor", v => { cfg.WindowCorner = v; Changed(); })));
            }
            else if (id == "ask")
            {
                sp.Children.Add(Grp("물어보기"));
                sp.Children.Add(new TextBlock { Text = "API 키 없이, 로그인되어 있는 내 Claude 계정으로 새 대화를 열어 질문을 미리 채워 줘요. 보내기를 누르면 검토 후 전송하면 됩니다.", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
                sp.Children.Add(SRow("어디서 열지", "앱이 없으면 자동으로 웹으로 열려요", Seg(new[] { new[] { "app", "Claude 앱" }, new[] { "web", "claude.ai 웹" } }, cfg.AskTarget == "web" ? "web" : "app", v => { cfg.AskTarget = v; Changed(); })));
                sp.Children.Add(SRow("복사한 텍스트 함께 보내기", "물어보기 창에서 기본으로 체크할지", Switch(cfg.AskIncludeClipboard, on => { cfg.AskIncludeClipboard = on; Changed(); })));
            }
            else if (id == "everything")
            {
                sp.Children.Add(Grp("검색 창"));
                sp.Children.Add(SRow("마지막 검색어 유지", "다시 열 때 검색어와 필터를 그대로", Switch(cfg.SearchKeepQuery, on => { cfg.SearchKeepQuery = on; Changed(); })));
                sp.Children.Add(SRow("기본 필터", "검색어를 유지하지 않을 때", Seg(FILTERS, cfg.SearchDefaultFilter.ToString(), v => { cfg.SearchDefaultFilter = int.Parse(v); Changed(); })));

                var panel = new SearchSettingsPanel(app, false);
                panel.AutoApply = true;
                sp.Children.Add(panel);
            }
            else if (id == "organize")
            {
                sp.Children.Add(Grp("정리"));
                string last = "";
                foreach (var sc in new[] { new[] { "kind", "종류별" }, new[] { "project", "프로젝트 표준" }, new[] { "date", "날짜별" }, new[] { "ext", "확장자별" } })
                    if (sc[0] == cfg.OrganizeLastScheme) last = " · 지난번: " + sc[1];
                sp.Children.Add(SRow("정리 방법", "실행할 때마다 종류별 · 프로젝트 표준 · 날짜별 · 확장자별 중에서 물어봐요" + last, null));
                sp.Children.Add(SRow("옛 버전은 _old 폴더로", "최종 · 최종2 · v1 · 수정처럼 같은 파일의 이전 버전 (처음 켜진 상태)", Switch(cfg.OrganizeOldVersions, on => { cfg.OrganizeOldVersions = on; Changed(); })));
                sp.Children.Add(SRow("하위 폴더도 정리", "처음 켜진 상태", Switch(cfg.OrganizeSubfolders, on => { cfg.OrganizeSubfolders = on; Changed(); })));
                string info = OrganizeWindow.LastInfo();
                var undoBtn = SBtn("되돌리기", false, false);
                undoBtn.IsEnabled = info != null;
                undoBtn.Click += (s, e) => { int n = OrganizeWindow.Undo(app); app.Toast(n > 0 ? "파일 " + n + "개를 원래 자리로 돌려놨어요" : "되돌릴 정리가 없어요", 2400); ShowTab(currentTab); };
                sp.Children.Add(SRow("마지막 정리 되돌리기", info ?? "되돌릴 정리가 없어요", undoBtn));
                sp.Children.Add(new TextBlock { Text = "파일은 지우지 않고 옮기기만 해요. 탐색기 창 위에서 링을 열면 마우스 아래 그 폴더가 자동으로 들어가요. 정리 창의 [탐색기 창 클릭]으로 다른 창을 고를 수도 있어요.", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap });
            }
            else if (id == "capture")
            {
                sp.Children.Add(Grp("캡처"));
                sp.Children.Add(new TextBlock { Text = "버블을 누르면 Windows 기본 화면 캡처(Win+Shift+S)가 열려요. 영역을 끌어서 고르면 클립보드에 복사되고, 아래 폴더에 PNG로 저장돼요.", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
                sp.Children.Add(SRow("캡처 후 자동 저장", "끄면 클립보드에만 복사돼요 (저장 안 함)", Switch(cfg.CaptureAutoSave, on => { cfg.CaptureAutoSave = on; Changed(); ShowTab(currentTab); })));
                sp.Children.Add(SRow("저장 형식", "PNG는 화질 그대로, JPG는 용량이 작아요", Seg(new[] { new[] { "png", "PNG" }, new[] { "jpg", "JPG" } }, (cfg.CaptureFormat ?? "png").ToLowerInvariant() == "jpg" ? "jpg" : "png", v => { cfg.CaptureFormat = v; Changed(); })));
                string folder = string.IsNullOrEmpty(cfg.CaptureFolder) ? Capture.DefaultFolder() + " (기본)" : cfg.CaptureFolder;
                var browse = SBtn("찾아보기…", false, false);
                var openBtn = SBtn("폴더 열기", false, false);
                var folderRow = SRow("저장 위치", folder, null);
                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                browse.Margin = new Thickness(0, 0, 6, 0);
                btns.Children.Add(browse);
                btns.Children.Add(openBtn);
                ((Grid)folderRow.Child).Children.Add(btns);
                Grid.SetColumn(btns, 1);
                browse.IsEnabled = cfg.CaptureAutoSave;
                browse.Click += (s, e) =>
                {
                    var dlg = new WinForms.FolderBrowserDialog { Description = "캡처를 저장할 폴더를 고르세요", ShowNewFolderButton = true };
                    if (!string.IsNullOrEmpty(cfg.CaptureFolder) && IO.Directory.Exists(cfg.CaptureFolder)) dlg.SelectedPath = cfg.CaptureFolder;
                    if (dlg.ShowDialog() == WinForms.DialogResult.OK) { cfg.CaptureFolder = dlg.SelectedPath; Changed(); ShowTab(currentTab); }
                };
                openBtn.Click += (s, e) => { try { string d = Capture.Folder(cfg); IO.Directory.CreateDirectory(d); Process.Start("explorer.exe", "\"" + d + "\""); } catch (Exception) { } };
                sp.Children.Add(folderRow);
                if (!string.IsNullOrEmpty(cfg.CaptureFolder))
                {
                    var reset = SBtn("기본 위치로", false, false);
                    reset.Click += (s, e) => { cfg.CaptureFolder = ""; Changed(); ShowTab(currentTab); };
                    sp.Children.Add(SRow("기본 위치", "사진\\ClaudeRing 캡처 폴더로 되돌려요", reset));
                }
            }
            else if (a.Handler == null)
            {
                sp.Children.Add(new TextBlock { Text = "이 기능을 만들면 여기에 기능별 설정이 생겨요.", FontSize = 11.5, Foreground = Theme.B(Theme.Muted), Margin = new Thickness(0, 14, 0, 0) });
            }
            return sp;
        }

        static bool IsAutoStart()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_KEY, false))
                    return k != null && k.GetValue("ClaudeRing") != null;
            }
            catch (Exception) { return false; }
        }

        static void SetAutoStart(bool on)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_KEY, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("ClaudeRing", "\"" + Process.GetCurrentProcess().MainModule.FileName + "\"");
                    else if (k.GetValue("ClaudeRing") != null) k.DeleteValue("ClaudeRing");
                }
            }
            catch (Exception ex) { RingApp.Log("자동 실행 설정 오류: " + ex.Message); }
        }
    }
}
