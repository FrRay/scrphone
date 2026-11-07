using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace PhoneLauncher {
  partial class MainForm : Form {
    readonly string toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "scrcpy");
    string configPath; Config cfg; Process session; readonly List<AppEntry> apps = new List<AppEntry>();
    ListBox devices = new ListBox(), appList = new ListBox(); TextBox search = new TextBox(), log = new TextBox(), width = new TextBox(), height = new TextBox(), dpi = new TextBox();
    Label status = new Label(), selected = new Label(), dependencies = new Label(); ComboBox mode = new ComboBox(), mouse = new ComboBox();
    CheckBox flex = new CheckBox(), keep = new CheckBox(), uhid = new CheckBox(), ime = new CheckBox(), screenOff = new CheckBox();
    Button launch, stop, refreshApps, restore, refreshDeviceButton; FlowLayoutPanel deviceTiles; string deviceTilesSignature=""; Label deviceFeedback = new Label(), actionFeedback = new Label(); bool lastDeviceRefreshSuccess, lastAppsRefreshSuccess; Label connection = new Label(), appState = new Label(); bool busy, loadingApps, refreshingDevices, changingDevices; int deviceGeneration; float uiScale; TableLayoutPanel shellLayout; Panel[] settingsPages; Panel workspace, settingsHost; readonly List<Button> navButtons = new List<Button>(); FlowLayoutPanel recentPanel; AppGrid appGrid; Label historyState = new Label(); readonly List<RecentItem> recent = new List<RecentItem>();
    // Keep native frame styles so DWM can draw the outer border and shadow.
    protected override CreateParams CreateParams {
      get { var parameters = base.CreateParams; parameters.Style |= 0x00C40000; return parameters; }
    }
    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      ApplyWindowFrame(true);
    }
    protected override void OnActivated(EventArgs e) { base.OnActivated(e); ApplyWindowFrame(true); }
    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); ApplyWindowFrame(false); }
    public MainForm() {
      AutoScaleDimensions = new SizeF(96F,96F); AutoScaleMode = AutoScaleMode.Dpi; Text = "手机应用启动器"; FormBorderStyle=FormBorderStyle.None; float displayScale; using(var display=Graphics.FromHwnd(IntPtr.Zero)) displayScale=display.DpiX/96F; uiScale=displayScale; MinimumSize = new Size((int)Math.Round(900*displayScale), (int)Math.Round(600*displayScale)); Size = MinimumSize; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 9F); Padding=new Padding((int)Math.Ceiling(6*displayScale));
      configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml"); cfg = LoadConfig();
      if (String.IsNullOrWhiteSpace(cfg.Adb)) cfg.Adb = Path.Combine(toolsPath, "adb.exe");
      if (String.IsNullOrWhiteSpace(cfg.Scrcpy)) cfg.Scrcpy = Path.Combine(toolsPath, "scrcpy.exe");
      LoadHistory(); BuildUI(); InstallTopDrag(this); ApplyConfig(); UpdateDependencies(); var deviceTimer=new System.Windows.Forms.Timer { Interval=10000 }; deviceTimer.Tick+=async(s,e)=>await RefreshDevices(); Shown += async (s,e) => { ApplyBackdrop(); await RefreshDevices(); if (!IsDisposed && !Disposing) deviceTimer.Start(); };
      FormClosing += (s,e) => { deviceTimer.Stop(); deviceTimer.Dispose(); SaveConfig(); try { if (session != null && !session.HasExited) session.CloseMainWindow(); } catch (InvalidOperationException) { } };
    }
    public void ToggleWindowMaximized(){if(WindowState==FormWindowState.Maximized)WindowState=FormWindowState.Normal;else{MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=FormWindowState.Maximized;}}
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
    bool IsTopDragPoint(Point p){return p.Y>=0 && p.Y<Padding.Top+(int)Math.Ceiling(32*uiScale) && p.X>=Padding.Left+(int)Math.Ceiling(100*uiScale) && p.X<ClientSize.Width-Padding.Right;}
    void InstallTopDrag(Control root){if(root is Panel || root is Label || root is TabPage){root.MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;var control=(Control)s;var point=PointToClient(control.PointToScreen(e.Location));if(!IsTopDragPoint(point))return;ReleaseCapture();SendMessage(Handle,0x00A1,(IntPtr)2,IntPtr.Zero);};}foreach(Control child in root.Controls)InstallTopDrag(child);}
    protected override void WndProc(ref Message m){if(m.Msg==0x83 && m.WParam!=IntPtr.Zero){m.Result=IntPtr.Zero;return;}if(m.Msg==0x84){long value=m.LParam.ToInt64();var point=PointToClient(new Point(unchecked((short)(value&0xffff)),unchecked((short)((value>>16)&0xffff))));int border=Math.Max(6,Padding.Left);if(WindowState==FormWindowState.Normal){bool left=point.X<border,right=point.X>=ClientSize.Width-border,top=point.Y<border,bottom=point.Y>=ClientSize.Height-border;if(top&&left){m.Result=(IntPtr)13;return;}if(top&&right){m.Result=(IntPtr)14;return;}if(bottom&&left){m.Result=(IntPtr)16;return;}if(bottom&&right){m.Result=(IntPtr)17;return;}if(left){m.Result=(IntPtr)10;return;}if(right){m.Result=(IntPtr)11;return;}if(top){m.Result=(IntPtr)12;return;}if(bottom){m.Result=(IntPtr)15;return;}}if(IsTopDragPoint(point)){m.Result=(IntPtr)2;return;}}base.WndProc(ref m);}
    Config LoadConfig() { try { using (var f = File.OpenRead(configPath)) return (Config)new XmlSerializer(typeof(Config)).Deserialize(f); } catch { return new Config(); } }
    void SaveConfig() { try { cfg.Dpi = Number(dpi.Text,240); cfg.Size = width.Text.Trim()+"x"+height.Text.Trim(); cfg.Flex=flex.Checked; cfg.Keep=keep.Checked; cfg.Uhid=uhid.Checked; cfg.Ime=ime.Checked; cfg.ScreenOff=screenOff.Checked; cfg.Mouse=mouse.Text; XmlStore.Save(configPath, cfg); } catch(Exception ex) { WriteLog("保存设置失败："+ex.Message); } }
    int Number(string s,int fallback) { int n; return Int32.TryParse(s,out n) ? n : fallback; }
    static Button Btn(string text, EventHandler action) { var b=new Button {Text=text, AutoSize=true, Height=34, Margin=new Padding(3), FlatStyle=FlatStyle.Flat, BackColor=Color.White, ForeColor=Color.FromArgb(35,40,48), Padding=new Padding(7,0,7,0)}; b.FlatAppearance.BorderColor=Color.FromArgb(216,220,226); b.Click+=action; return b; }
    static Label Lab(string text) { return new Label {Text=text, AutoSize=true, Margin=new Padding(4,9,4,4)}; }
    static FlowLayoutPanel Flow() { return new FlowLayoutPanel {Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(16), BackColor=Color.White}; }
  }
}
