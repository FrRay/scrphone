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

  [Serializable] public class Config {
    public string Adb = "";
    public string Scrcpy = ""; public string Address = ""; public int Dpi = 240;
    public string Size = "1024x768"; public bool Flex = true; public bool Keep = true;
    public bool Uhid = false; public bool Ime = false; public bool ScreenOff = false;
    public string Mouse = "b---：右键返回，其余禁用";
  }
  class Device { public string Serial, State, Model, Identity; public override string ToString() { string transport=Serial!=null&&(Serial.Contains(":")||Serial.Contains("._adb-tls-connect._tcp"))?"无线":"USB";return Model+"（"+transport+"）"+(State=="device"?"":State=="offline"?"（离线）":"（等待授权）"); } }
  static class Corners {
    public static GraphicsPath Path(Rectangle bounds,int radius){var path=new GraphicsPath();int d=Math.Max(2,Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height)));path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;}
    public static void Apply(Control control,int radius){Action update=()=>{if(control.Width<3||control.Height<3)return;using(var path=Path(new Rectangle(0,0,control.Width,control.Height),radius)){var old=control.Region;control.Region=new Region(path);if(old!=null)old.Dispose();}};control.SizeChanged+=(s,e)=>update();update();}
  }
  class RecentButton : Button {
    bool hovered; public bool Selected; public RecentButton(string label,EventHandler clicked){Text=label;AccessibleName=label;Width=120;Height=60;Margin=new Padding(3);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;BackColor=Color.White;Cursor=Cursors.Hand;DoubleBuffered=true;Click+=clicked;}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var bounds=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));using(var path=Corners.Path(bounds,10)){using(var fill=new SolidBrush(Selected?Color.FromArgb(220,237,252):hovered?Color.FromArgb(234,243,252):Color.White))e.Graphics.FillPath(fill,path);using(var pen=new Pen(Selected?Color.FromArgb(0,103,192):Color.FromArgb(205,214,225),Selected?2:1))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,Rectangle.Inflate(bounds,-6,-2),Color.FromArgb(31,36,43),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);}
  }
  class DeviceTile : Button {
    public readonly Device Device; public bool CurrentDevice;
    public DeviceTile(Device device){Device=device;Text=device.ToString();AccessibleName=device.ToString();Width=235;Height=72;Margin=new Padding(0,0,10,4);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.White;UseVisualStyleBackColor=false;DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var rect=new Rectangle(1,1,Width-3,Height-3);using(var path=Corners.Path(rect,10)){using(var fill=new SolidBrush(CurrentDevice?Color.FromArgb(238,246,255):Color.White))e.Graphics.FillPath(fill,path);using(var border=new Pen(CurrentDevice?Color.FromArgb(0,103,192):Color.FromArgb(215,222,230),CurrentDevice?2:1))e.Graphics.DrawPath(border,path);}using(var pen=new Pen(CurrentDevice?Color.FromArgb(0,103,192):Color.FromArgb(90,100,110),1.6f)){e.Graphics.DrawRectangle(pen,14,14,23,39);e.Graphics.DrawLine(pen,20,18,31,18);e.Graphics.DrawLine(pen,23,49,28,49);}var title=new Rectangle(50,13,Width-58,22);var subtitle=new Rectangle(50,36,Width-58,20);TextRenderer.DrawText(e.Graphics,Device.Model,Font,title,Color.FromArgb(31,36,43),TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);string info=Device.Serial!=null&&(Device.Serial.Contains(":")||Device.Serial.Contains("._adb-tls-connect._tcp"))?"无线":"USB";if(Device.State!="device")info+=Device.State=="offline"?"（离线）":"（等待授权）";TextRenderer.DrawText(e.Graphics,info,Font,subtitle,Color.FromArgb(75,88,105),TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);}
  }
  [Serializable] public class AppEntry { public string Name, Package; public override string ToString() { return Name; } }
  class LegacyAppGrid : ScrollableControl {
    readonly List<AppEntry> entries=new List<AppEntry>(); int columns=1,cardWidth=120; const int gap=12, cardHeight=60, rowHeight=74; string selectedPackage; int hover=-1;
    public event Action<AppEntry> ItemSelected; public event Action<AppEntry> ItemActivated;
    public LegacyAppGrid(){AutoScroll=true;BackColor=Color.White;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.Selectable,true);TabStop=true;AccessibleName="全部应用";AccessibleRole=AccessibleRole.List;}
    protected override AccessibleObject CreateAccessibilityInstance(){return new GridAccessible(this);}
    class GridAccessible : ControlAccessibleObject {
      readonly LegacyAppGrid grid; public GridAccessible(LegacyAppGrid owner):base(owner){grid=owner;} public override AccessibleRole Role {get{return AccessibleRole.List;}} public override string Name {get{return "全部应用";} set{}}
      public override int GetChildCount(){return grid.entries.Count;} public override AccessibleObject GetChild(int index){return index>=0&&index<grid.entries.Count?new ItemAccessible(grid,index):null;}
    }
    class ItemAccessible : AccessibleObject {
      readonly LegacyAppGrid grid; readonly int index; public ItemAccessible(LegacyAppGrid owner,int i){grid=owner;index=i;} public override string Name{get{return index<grid.entries.Count?grid.entries[index].Name:"";}set{}} public override AccessibleRole Role{get{return AccessibleRole.ListItem;}} public override string DefaultAction{get{return "启动";}} public override AccessibleObject Parent{get{return grid.AccessibilityObject;}}
      public override Rectangle Bounds{get{if(index>=grid.entries.Count)return Rectangle.Empty;var rect=grid.BoundsFor(index);return new Rectangle(grid.PointToScreen(rect.Location),rect.Size);}}
      public override AccessibleStates State{get{if(index>=grid.entries.Count)return AccessibleStates.Unavailable;var state=AccessibleStates.Selectable|AccessibleStates.Focusable;if(grid.entries[index].Package==grid.selectedPackage)state|=AccessibleStates.Selected;if(!grid.ClientRectangle.IntersectsWith(grid.BoundsFor(index)))state|=AccessibleStates.Offscreen;return state;}}
      public override void Select(AccessibleSelection flags){if(index>=grid.entries.Count)return;if((flags&(AccessibleSelection.TakeFocus|AccessibleSelection.TakeSelection))!=0){grid.Focus();if(grid.ItemSelected!=null)grid.ItemSelected(grid.entries[index]);grid.EnsureVisible(index);}}
      public override void DoDefaultAction(){if(index>=grid.entries.Count)return;if(grid.ItemSelected!=null)grid.ItemSelected(grid.entries[index]);if(grid.ItemActivated!=null)grid.ItemActivated(grid.entries[index]);}
    }
    public void SetItems(IEnumerable<AppEntry> items){entries.Clear();entries.AddRange(items);hover=-1;UpdateLayout();Invalidate();}
    public void SetSelected(AppEntry item){selectedPackage=item==null?null:item.Package;Invalidate();}
    void UpdateLayout(){int available=Math.Max(1,ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);columns=Math.Max(1,(available+gap)/(120+gap));cardWidth=Math.Max(1,available/columns-gap);int rows=(entries.Count+columns-1)/columns;AutoScrollMinSize=new Size(0,rows*rowHeight+8);}
    protected override void OnResize(EventArgs e){base.OnResize(e);UpdateLayout();Invalidate();}
    protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);Invalidate();}
    Rectangle BoundsFor(int index){return new Rectangle((index%columns)*(cardWidth+gap), (index/columns)*rowHeight+AutoScrollPosition.Y,cardWidth,cardHeight);}
    int Hit(Point p){int x=p.X,y=p.Y-AutoScrollPosition.Y;if(x<0||y<0)return -1;int col=x/(cardWidth+gap),row=y/rowHeight;if(col>=columns||x%(cardWidth+gap)>=cardWidth||y%rowHeight>=cardHeight)return -1;int index=row*columns+col;return index<entries.Count?index:-1;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);int first=Math.Max(0,(-AutoScrollPosition.Y/rowHeight)*columns);int last=Math.Min(entries.Count,first+((ClientSize.Height/rowHeight)+2)*columns);for(int i=first;i<last;i++){var r=BoundsFor(i);bool active=entries[i].Package==selectedPackage;Color fill=active?Color.FromArgb(220,237,252):i==hover?Color.FromArgb(234,243,252):Color.White;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var border=Rectangle.Inflate(r,-1,-1);if(border.Width>0&&border.Height>0){using(var path=Corners.Path(border,10)){using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);using(var pen=new Pen(active?Color.FromArgb(0,103,192):Color.FromArgb(205,214,225),active?2:1))e.Graphics.DrawPath(pen,path);}}var textRect=Rectangle.Inflate(r,-5,-3);TextRenderer.DrawText(e.Graphics,entries[i].Name,Font,textRect,Color.FromArgb(31,36,43),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int next=Hit(e.Location);if(next!=hover){hover=next;Cursor=hover>=0?Cursors.Hand:Cursors.Default;Invalidate();}}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);if(hover!=-1){hover=-1;Invalidate();}}
    protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button!=MouseButtons.Left)return;Focus();int index=Hit(e.Location);if(index>=0&&ItemSelected!=null)ItemSelected(entries[index]);}
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.Button!=MouseButtons.Left)return;int index=Hit(e.Location);if(index>=0&&ItemActivated!=null)ItemActivated(entries[index]);}
    protected override bool IsInputKey(Keys keyData){var key=keyData&Keys.KeyCode;return key==Keys.Up||key==Keys.Down||key==Keys.Left||key==Keys.Right||base.IsInputKey(keyData);}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(entries.Count==0)return;int index=entries.FindIndex(x=>x.Package==selectedPackage);if(index<0)index=0;else if(e.KeyCode==Keys.Left)index--;else if(e.KeyCode==Keys.Right)index++;else if(e.KeyCode==Keys.Up)index-=columns;else if(e.KeyCode==Keys.Down)index+=columns;else if(e.KeyCode==Keys.Enter){if(ItemActivated!=null)ItemActivated(entries[index]);e.Handled=true;return;}else return;index=Math.Max(0,Math.Min(entries.Count-1,index));if(ItemSelected!=null)ItemSelected(entries[index]);EnsureVisible(index);e.Handled=true;}
    public void EnsureVisible(int index){if(index<0||index>=entries.Count)return;int top=(index/columns)*rowHeight;int current=-AutoScrollPosition.Y;if(top<current)AutoScrollPosition=new Point(0,top);else if(top+cardHeight>current+ClientSize.Height)AutoScrollPosition=new Point(0,Math.Max(0,top+cardHeight-ClientSize.Height));Invalidate();}
  }
  class CaptionBar : Control {
    readonly Form owner; readonly float scale; int hovered=-1; readonly ToolTip tip=new ToolTip{InitialDelay=500};
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
    public CaptionBar(Form form,float dpiScale){owner=form;scale=dpiScale;Height=(int)Math.Round(32*scale);Dock=DockStyle.Top;Margin=Padding.Empty;BackColor=Color.FromArgb(243,247,251);DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleName="窗口标题栏";}
    Rectangle ButtonBounds(int i){int size=(int)Math.Round(14*scale);int gap=(int)Math.Round(10*scale);int left=(int)Math.Round(14*scale);return new Rectangle(left+i*(size+gap),(Height-size)/2,size,size);}
    int Hit(Point p){for(int i=0;i<3;i++){var r=Rectangle.Inflate(ButtonBounds(i),(int)(4*scale),(int)(4*scale));if(r.Contains(p))return i;}return -1;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color[] colors={Color.FromArgb(255,95,87),Color.FromArgb(40,200,64),Color.FromArgb(254,188,46)};for(int i=0;i<3;i++){var r=ButtonBounds(i);using(var b=new SolidBrush(colors[i]))e.Graphics.FillEllipse(b,r);using(var p=new Pen(Color.FromArgb(45,0,0,0)))e.Graphics.DrawEllipse(p,r);if(hovered==i){using(var pen=new Pen(Color.FromArgb(125,35,35,35),Math.Max(1,scale))){float x=r.X,y=r.Y,w=r.Width,h=r.Height;if(i==0){e.Graphics.DrawLine(pen,x+w*.32f,y+h*.32f,x+w*.68f,y+h*.68f);e.Graphics.DrawLine(pen,x+w*.68f,y+h*.32f,x+w*.32f,y+h*.68f);}else if(i==2)e.Graphics.DrawLine(pen,x+w*.28f,y+h*.52f,x+w*.72f,y+h*.52f);else e.Graphics.DrawRectangle(pen,x+w*.3f,y+h*.3f,w*.4f,h*.4f);}}}}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=-1;tip.SetToolTip(this,"");Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int hit=Hit(e.Location);if(hit!=hovered){hovered=hit;tip.SetToolTip(this,hit==0?"关闭":hit==2?"最小化":hit==1?(owner.WindowState==FormWindowState.Maximized?"还原":"最大化"):"");Invalidate();}}
    void ToggleMax(){((MainForm)owner).ToggleWindowMaximized();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;int hit=Hit(e.Location);if(hit==0){owner.Close();return;}if(hit==2){owner.WindowState=FormWindowState.Minimized;return;}if(hit==1){ToggleMax();return;}if(e.Clicks>=2){ToggleMax();return;}ReleaseCapture();SendMessage(owner.Handle,0x00A1,(IntPtr)2,IntPtr.Zero);}
  }
  [Serializable] public class RecentItem { public string Identity, Name, Package; public DateTime Used; }
  [Serializable] public class AppCache { public string Identity; public DateTime Updated; public List<AppEntry> Items = new List<AppEntry>(); }
  class SearchField : Panel {
    readonly TextBox input; static readonly Color Fill=Color.FromArgb(242,245,249);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string text);
    public SearchField(TextBox box) { input=box; DoubleBuffered=true; BackColor=Color.White; input.BorderStyle=BorderStyle.None; input.BackColor=Fill; input.ForeColor=Color.FromArgb(40,45,50); input.HandleCreated+=(s,e)=>SendMessage(input.Handle,0x1501,IntPtr.Zero,"搜索应用"); Controls.Add(input); Cursor=Cursors.IBeam; Click+=(s,e)=>input.Focus(); }
    protected override void OnResize(EventArgs e){base.OnResize(e);input.SetBounds(12,Math.Max(5,(Height-input.PreferredHeight)/2),Math.Max(20,Width-48),input.PreferredHeight);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(0,0,Width-1,Height-1);if(r.Width<12||r.Height<12)return;int d=10;using(var path=new GraphicsPath()){path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();using(var fill=new SolidBrush(Fill))e.Graphics.FillPath(fill,path);using(var border=new Pen(Color.FromArgb(222,226,231)))e.Graphics.DrawPath(border,path);}float x=Width-27,y=(Height-13)/2f;using(var pen=new Pen(Color.FromArgb(48,53,59),1.5f)){e.Graphics.DrawEllipse(pen,x,y,10,10);e.Graphics.DrawLine(pen,x+8,y+8,x+13,y+13);}}
  }
  static class Program { [DllImport("user32.dll")] static extern bool SetProcessDPIAware(); [STAThread] static void Main() { SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); } }
}
