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
  partial class MainForm {
    void BuildUI() {
      BackColor=Color.FromArgb(243,247,251); ForeColor=Color.FromArgb(31,36,43); Font=new Font("Microsoft YaHei UI",9F);
      var outer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=Color.FromArgb(243,247,251)};outer.RowStyles.Add(new RowStyle(SizeType.Absolute,(int)Math.Round(32*uiScale)));outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(outer);var caption=new CaptionBar(this,uiScale){Dock=DockStyle.Fill,Margin=Padding.Empty};outer.Controls.Add(caption,0,0);var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,BackColor=Color.FromArgb(243,247,251)};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,200));frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));outer.Controls.Add(frame,0,1);
      var sidebar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12,5,10,10),Margin=new Padding(8,8,12,8),BackColor=Color.White,AutoScroll=true};Corners.Apply(sidebar,20);frame.Controls.Add(sidebar,0,0);sidebar.Controls.Add(new Label{Text="ScrPhone",Font=new Font(Font.FontFamily,13,FontStyle.Bold),ForeColor=Color.FromArgb(36,49,67),AutoSize=true,Margin=new Padding(8,3,0,22)});
      AddNav(sidebar,"应用",()=>ShowHome());AddNav(sidebar,"设备与连接",()=>ShowSettings(0));AddNav(sidebar,"启动参数",()=>ShowSettings(1));AddNav(sidebar,"运行日志",()=>ShowSettings(2));
      workspace=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(243,247,251)};frame.Controls.Add(workspace,1,0);
      shellLayout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(16,8,16,8),BackColor=Color.FromArgb(243,247,251)};shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));shellLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,320));workspace.Controls.Add(shellLayout);
      var listArea=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.White,Padding=new Padding(16,12,16,12),Margin=new Padding(0,0,0,10)};Corners.Apply(listArea,20);listArea.RowStyles.Add(new RowStyle(SizeType.Absolute,42));listArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));var title=new FlowLayoutPanel {Dock=DockStyle.Fill};title.Controls.Add(new Label{Text="全部应用",AutoSize=true,Font=new Font(Font.FontFamily,15,FontStyle.Bold),Margin=new Padding(0,0,16,0)});appState.AutoSize=true;appState.ForeColor=Color.FromArgb(75,88,105);appState.Margin=new Padding(3,9,0,0);appState.Text="连接手机后自动获取应用列表";title.Controls.Add(appState);listArea.Controls.Add(title,0,0);
      appList.Visible=false;appList.SelectedIndexChanged+=(s,e)=>{var x=appList.SelectedItem as AppEntry;selected.Text=x==null?"请选择应用":x.Name;UpdateState();UpdateCardSelection();};Controls.Add(appList);
      appGrid=new AppGrid{Dock=DockStyle.Fill};appGrid.ItemSelected+=(item)=>appList.SelectedItem=item;appGrid.ItemActivated+=async(item)=>{appList.SelectedItem=item;if(launch.Enabled)await Launch();};listArea.Controls.Add(appGrid,0,1);shellLayout.Controls.Add(listArea,0,0);
      var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Color.White,Padding=new Padding(16,10,16,10),Margin=Padding.Empty};bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,44));bottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,50));Corners.Apply(bottom,20);shellLayout.Controls.Add(bottom,0,1);search.Font=new Font(Font.FontFamily,9);search.TextChanged+=(s,e)=>FilterApps();var searchField=new SearchField(search){Dock=DockStyle.Fill,Margin=new Padding(0,3,0,3)};bottom.Controls.Add(searchField,0,0);

      recentPanel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=true,AutoScroll=true,Padding=new Padding(0,2,0,0),BackColor=Color.White};bottom.Controls.Add(recentPanel,0,1);
      var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));status.Dock=DockStyle.Fill;status.Text="准备就绪";status.ForeColor=Color.FromArgb(75,88,105);footer.Controls.Add(status,0,0);selected.Dock=DockStyle.Fill;selected.AutoEllipsis=true;selected.Text="请选择应用";selected.TextAlign=ContentAlignment.MiddleRight;footer.Controls.Add(selected,1,0);launch=Btn("开始",async(s,e)=>await Launch());launch.Dock=DockStyle.None;launch.Width=90;launch.Height=34;launch.Anchor=AnchorStyles.Top|AnchorStyles.Right;launch.FlatAppearance.BorderSize=0;Corners.Apply(launch,8);footer.Controls.Add(launch,2,0);bottom.Controls.Add(footer,0,2);
      BuildSettings();ShowHome();UpdateState();
    }
    void BuildSettings(){
      settingsHost=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,8,16,8),BackColor=Color.FromArgb(243,247,251),Visible=false};workspace.Controls.Add(settingsHost);var settingsCard=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(16)};Corners.Apply(settingsCard,20);settingsHost.Controls.Add(settingsCard);
      var dev=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var options=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Visible=false};var logs=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Visible=false};settingsPages=new[]{dev,options,logs};settingsCard.Controls.AddRange(settingsPages);
      var d=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Padding=new Padding(28,24,28,18),BackColor=Color.White};
      d.RowStyles.Add(new RowStyle(SizeType.Absolute,44));d.RowStyles.Add(new RowStyle(SizeType.Absolute,32));d.RowStyles.Add(new RowStyle(SizeType.Absolute,36));d.RowStyles.Add(new RowStyle(SizeType.Absolute,100));d.RowStyles.Add(new RowStyle(SizeType.Absolute,54));d.RowStyles.Add(new RowStyle(SizeType.Absolute,48));d.RowStyles.Add(new RowStyle(SizeType.Absolute,40));d.RowStyles.Add(new RowStyle(SizeType.Percent,100));d.RowStyles.Add(new RowStyle(SizeType.Absolute,30));dev.Controls.Add(d);
      d.Controls.Add(new Label{Text="设备与连接",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,15,FontStyle.Bold)},0,0);
      d.Controls.Add(new Label{Text="请先开启手机的无线调试，也可通过 USB 连接。",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(75,88,105)},0,1);
      connection.Dock=DockStyle.Fill;connection.TextAlign=ContentAlignment.MiddleLeft;d.Controls.Add(connection,0,2);
      devices.Visible=false;Controls.Add(devices);devices.SelectedIndexChanged+=async(s,e)=>{if(!changingDevices)await DeviceChanged();};deviceTiles=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,BackColor=Color.White};d.Controls.Add(deviceTiles,0,3);
      d.Controls.Add(new Label{Text="首次连接时，请先完成设备配对。\r\n※ 配对成功后，ScrPhone 将自动连接已开启「无线调试」的 Android 设备。",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(75,88,105),TextAlign=ContentAlignment.MiddleLeft},0,4);
      var db=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};db.Controls.Add(Btn("配对新设备",async(s,e)=>await PairDiscovery()));db.Controls.Add(Btn("搜索并连接",async(s,e)=>await StartWireless()));db.Controls.Add(Btn("手动连接",async(s,e)=>await Connect()));refreshDeviceButton=Btn("刷新列表",async(s,e)=>await RefreshDevicesFeedback());db.Controls.Add(refreshDeviceButton);d.Controls.Add(db,0,5);
      deviceFeedback.Dock=DockStyle.Fill;deviceFeedback.ForeColor=Color.FromArgb(75,88,105);deviceFeedback.TextAlign=ContentAlignment.MiddleLeft;status.TextChanged+=(s,e)=>{string msg=status.Text;if(msg.IndexOf("失败")>=0||msg.IndexOf("成功")>=0||msg.IndexOf("未发现")>=0||msg.IndexOf("正在")>=0||msg.IndexOf("未完成")>=0)deviceFeedback.Text=msg;};d.Controls.Add(deviceFeedback,0,6);
      dependencies.Dock=DockStyle.Fill;dependencies.ForeColor=Color.FromArgb(83,96,113);d.Controls.Add(dependencies,0,8);
      var panel=Flow();options.Controls.Add(panel);
      panel.Controls.Add(Section("工具路径"));
      var adbPath=new TextBox{Text=cfg.Adb,Width=400};var scrcpyPath=new TextBox{Text=cfg.Scrcpy,Width=400};
      var adbRow=new FlowLayoutPanel{AutoSize=true,Width=550,WrapContents=false};adbRow.Controls.Add(Lab("ADB"));adbRow.Controls.Add(adbPath);adbRow.Controls.Add(Btn("浏览",(s,e)=>Browse(adbPath)));panel.Controls.Add(adbRow);
      var scrcpyRow=new FlowLayoutPanel{AutoSize=true,Width=550,WrapContents=false};scrcpyRow.Controls.Add(Lab("scrcpy"));scrcpyRow.Controls.Add(scrcpyPath);scrcpyRow.Controls.Add(Btn("浏览",(s,e)=>Browse(scrcpyPath)));panel.Controls.Add(scrcpyRow);
      panel.Controls.Add(Btn("保存工具路径",(s,e)=>{cfg.Adb=adbPath.Text.Trim();cfg.Scrcpy=scrcpyPath.Text.Trim();SaveConfig();UpdateDependencies();status.Text="工具路径已保存";}));
      panel.Controls.Add(Lab("显示模式"));mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Width=350;mode.Items.AddRange(new object[]{"新建虚拟屏幕（--new-display）","镜像手机主屏幕（默认）"});mode.SelectedIndex=0;mode.SelectedIndexChanged+=(s,e)=>{ToggleMode();UpdateState();};panel.Controls.Add(mode);
      var sizes=new FlowLayoutPanel{AutoSize=true,Width=520};sizes.Controls.Add(Lab("宽度"));width.Width=65;sizes.Controls.Add(width);sizes.Controls.Add(Lab("高度"));height.Width=65;sizes.Controls.Add(height);sizes.Controls.Add(Lab("密度"));dpi.Width=65;sizes.Controls.Add(dpi);sizes.Controls.Add(Lab("DPI"));panel.Controls.Add(sizes);
      flex.Text="动态调整虚拟屏幕尺寸（--flex-display）";keep.Text="保持设备活动（--keep-active）";uhid.Text="模拟实体键盘（--keyboard=uhid）";ime.Text="在虚拟屏幕显示输入法（--display-ime-policy=local）";screenOff.Text="启动后关闭手机主屏幕（adb display power-off 0）";foreach(var c in new[]{flex,keep,uhid,ime,screenOff}){c.AutoSize=true;panel.Controls.Add(c);}panel.Controls.Add(Lab("鼠标按键（--mouse-bind）"));mouse.DropDownStyle=ComboBoxStyle.DropDownList;mouse.Width=400;mouse.Items.AddRange(new object[]{"右键返回，其余按键禁用（b---）","禁用右键、中键和侧键（----）","返回、主页、多任务和通知（默认 bhsn）"});panel.Controls.Add(mouse);
      var buttons=new FlowLayoutPanel{AutoSize=true,Width=500};stop=Btn("结束会话",(s,e)=>StopSession());restore=Btn("恢复手机屏幕",async(s,e)=>await RestoreScreen());refreshApps=Btn("重新获取应用列表",async(s,e)=>await RefreshApps());buttons.Controls.Add(stop);buttons.Controls.Add(restore);buttons.Controls.Add(refreshApps);actionFeedback.AutoSize=true;actionFeedback.MaximumSize=new Size(550,0);actionFeedback.ForeColor=Color.FromArgb(75,88,105);actionFeedback.Margin=new Padding(4,8,4,4);actionFeedback.Text="当前没有运行中的投屏会话。";panel.Controls.Add(actionFeedback);panel.Controls.Add(buttons);
      var tips=new ToolTip{AutoPopDelay=15000,InitialDelay=400};
      tips.SetToolTip(mode,"虚拟屏幕会创建独立显示区域；镜像模式显示手机主屏幕。应用对虚拟屏幕的支持可能不同。");
      tips.SetToolTip(width,"虚拟屏幕的初始宽度，单位为像素。启用动态尺寸后，可通过拖动窗口调整。");
      tips.SetToolTip(height,"虚拟屏幕的初始高度，单位为像素。应用可能要求特定的屏幕方向。");
      tips.SetToolTip(dpi,"虚拟屏幕的显示密度。降低 DPI 通常会缩小界面元素；效果取决于应用布局。");
      tips.SetToolTip(flex,"拖动电脑窗口时，同步调整虚拟屏幕的尺寸。部分应用可能限制方向或布局。");
      tips.SetToolTip(keep,"定期向系统发送活动信号，减少因闲置而休眠。此选项不会关闭手机主屏幕。");
      tips.SetToolTip(uhid,"将电脑键盘模拟为手机的实体键盘，可用于无线连接。首次使用可能需要在手机上设置键盘布局。");
      tips.SetToolTip(ime,"请求将手机输入法显示在虚拟屏幕。实际效果取决于手机系统和输入法。");
      tips.SetToolTip(screenOff,"投屏启动后，向手机发送关闭物理主屏幕的请求。可使用「恢复手机屏幕」重新开启。");
      tips.SetToolTip(mouse,"设置右键、中键和两个侧键的功能。返回键可能退出当前虚拟屏幕中的应用。");

      log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;log.Dock=DockStyle.Fill;log.Font=new Font("Consolas",9);logs.Controls.Add(log);
    }
    void AddNav(FlowLayoutPanel sidebar,string text,Action action){var button=Btn(text,(s,e)=>{navigationRevision++;action();});button.AutoSize=false;button.Width=150;button.Height=42;button.Margin=new Padding(0,2,0,2);button.TextAlign=ContentAlignment.MiddleLeft;button.Padding=new Padding(12,0,0,0);button.FlatAppearance.BorderSize=0;button.BackColor=sidebar.BackColor;button.UseVisualStyleBackColor=false;Corners.Apply(button,10);sidebar.Controls.Add(button);navButtons.Add(button);}
    void SelectNav(int index){for(int i=0;i<navButtons.Count;i++){bool chosen=i==index;navButtons[i].BackColor=chosen?Color.FromArgb(233,243,253):Color.White;navButtons[i].ForeColor=chosen?Color.FromArgb(0,95,184):Color.FromArgb(55,65,78);}}
    void ShowHome(){if(settingsHost!=null)settingsHost.Visible=false;shellLayout.Visible=true;shellLayout.BringToFront();SelectNav(0);}
    void ShowSettings(int tab){SaveConfig();for(int i=0;i<settingsPages.Length;i++)settingsPages[i].Visible=i==tab;shellLayout.Visible=false;settingsHost.Visible=true;settingsHost.BringToFront();SelectNav(tab==0?1:tab==1?2:3);}
  }
}
