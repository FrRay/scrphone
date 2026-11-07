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
    string HistoryPath(){return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"history.xml");}
    void LoadHistory(){try{using(var f=File.OpenRead(HistoryPath())){var data=(List<RecentItem>)new XmlSerializer(typeof(List<RecentItem>)).Deserialize(f);recent.AddRange(data);}}catch{}}
    void SaveHistory(){try{XmlStore.Save(HistoryPath(),recent);}catch(Exception ex){WriteLog("保存历史失败："+ex.Message);}}
    void RecordHistory(string identity,AppEntry app){if(String.IsNullOrEmpty(identity)||app==null)return;recent.RemoveAll(x=>x.Identity==identity&&x.Package==app.Package);recent.Insert(0,new RecentItem{Identity=identity,Name=app.Name,Package=app.Package,Used=DateTime.Now});var excess=recent.Where(x=>x.Identity==identity).Skip(48).ToList();foreach(var x in excess)recent.Remove(x);SaveHistory();RefreshHistory();}
    void RefreshHistory(){if(recentPanel==null)return;while(recentPanel.Controls.Count>0){var control=recentPanel.Controls[0];recentPanel.Controls.RemoveAt(0);control.Dispose();}var device=Current();var list=device==null||String.IsNullOrEmpty(device.Identity)?new List<RecentItem>():recent.Where(x=>x.Identity==device.Identity).OrderByDescending(x=>x.Used).Take(48).ToList();historyState.Text=list.Count==0?"暂无记录":"最近启动的应用";foreach(var item in list){var entry=item;var button=new RecentButton(entry.Name,(s,e)=>{search.Text="";var target=appList.Items.Cast<AppEntry>().FirstOrDefault(x=>x.Package==entry.Package);if(target!=null){appList.SelectedItem=target;appGrid.EnsureVisible(appList.SelectedIndex);}else status.Text="当前列表中没有该应用，请重新获取应用列表。";});button.Tag=entry.Package;recentPanel.Controls.Add(button);}UpdateCardSelection();}
    static Label Section(string text){return new Label {Text=text,AutoSize=true,Font=new Font("Microsoft YaHei UI",11F,FontStyle.Bold),Margin=new Padding(3,5,3,4)};}
    void ApplyConfig() { var m=Regex.Match(cfg.Size??"",@"^(\d+)x(\d+)$");width.Text=m.Success?m.Groups[1].Value:"1024";height.Text=m.Success?m.Groups[2].Value:"768";dpi.Text=cfg.Dpi.ToString();flex.Checked=cfg.Flex;keep.Checked=cfg.Keep;uhid.Checked=cfg.Uhid;ime.Checked=cfg.Ime;screenOff.Checked=cfg.ScreenOff;mouse.SelectedItem=cfg.Mouse;if(mouse.SelectedIndex<0) { if(cfg.Mouse=="全部禁用"||cfg.Mouse=="----：右键、中键和侧键禁用") mouse.SelectedIndex=1; else if(cfg.Mouse=="scrcpy 默认"||cfg.Mouse=="默认 bhsn：返回、主页、多任务、通知") mouse.SelectedIndex=2; else mouse.SelectedIndex=0; } }
    void ToggleMode(){ bool v=mode.SelectedIndex==0; foreach(Control c in new Control[]{width,height,dpi,flex,ime})c.Enabled=v; }
    void WriteLog(string s){ if(IsDisposed)return; if(InvokeRequired){try{BeginInvoke((Action)(()=>WriteLog(s)));}catch{}return;} log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine); }
    void UpdateDependencies(){dependencies.AutoSize=true;dependencies.Margin=new Padding(4,9,12,0);dependencies.Text="ADB: "+(File.Exists(cfg.Adb)?"可用":"未找到")+"   scrcpy: "+(File.Exists(cfg.Scrcpy)?"可用":"未找到");}
    void Browse(TextBox target){using(var d=new OpenFileDialog{Filter="程序文件 (*.exe)|*.exe"})if(d.ShowDialog(this)==DialogResult.OK)target.Text=d.FileName;}
    async Task<string> Run(string file, string args, int timeout = 15000) {
      string adbPath = cfg.Adb;
      return await Task.Run(() => ProcessRunner.Run(file, args, adbPath, timeout));
    }
    Device Current(){return devices.SelectedItem as Device;}
    bool Ready(){ if(Current()==null || Current().State!="device"){MessageBox.Show("请先连接手机，并选择已连接的设备。");return false;}return true; }
    string CachePath(string identity){ if(String.IsNullOrWhiteSpace(identity))return null;byte[] bytes;using(var sha=System.Security.Cryptography.SHA256.Create())bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(identity));return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache",BitConverter.ToString(bytes).Replace("-","")+".xml"); }
    AppCache LoadCache(string identity){try{string path=CachePath(identity);if(path==null)return null;using(var f=File.OpenRead(path)){var c=(AppCache)new XmlSerializer(typeof(AppCache)).Deserialize(f);return c.Identity==identity?c:null;}}catch{return null;}}
    void SaveCache(string identity,List<AppEntry> entries){try{string path=CachePath(identity);if(path==null)return;Directory.CreateDirectory(Path.GetDirectoryName(path));XmlStore.Save(path,new AppCache{Identity=identity,Updated=DateTime.Now,Items=entries});}catch(Exception ex){WriteLog("应用缓存保存失败："+ex.Message);}}
    void UpdateState(){bool connected=Current()!=null&&Current().State=="device";bool active=session!=null&&!session.HasExited; if(connection!=null){connection.Text=Current()==null?"●  未连接":connected?"●  已连接":Current().State=="unauthorized"?"●  等待手机授权":"●  设备离线";connection.ForeColor=connected?Color.FromArgb(38,143,91):Color.FromArgb(125,132,144);}if(refreshApps!=null)refreshApps.Enabled=connected&&!loadingApps;if(search!=null)search.Enabled=apps.Count>0;if(launch!=null)launch.Enabled=connected&&!busy&&!active&&(mode.SelectedIndex!=0||appList.SelectedItem!=null);if(launch!=null){launch.BackColor=launch.Enabled?Color.FromArgb(0,113,227):Color.FromArgb(226,229,233);launch.ForeColor=launch.Enabled?Color.White:Color.FromArgb(140,145,153);}if(stop!=null)stop.Enabled=active;if(restore!=null)restore.Enabled=connected;UpdateDeviceTiles();}
    void UpdateDeviceTiles(){if(deviceTiles==null)return;var current=Current();string signature=String.Join("|",devices.Items.Cast<Device>().Select(x=>x.Serial+":"+x.State+":"+x.Model).ToArray())+"#"+(current==null?"":current.Serial);if(signature==deviceTilesSignature)return;deviceTilesSignature=signature;deviceTiles.SuspendLayout();while(deviceTiles.Controls.Count>0){var old=deviceTiles.Controls[0];deviceTiles.Controls.RemoveAt(0);old.Dispose();}foreach(Device item in devices.Items){var entry=item;var tile=new DeviceTile(entry){CurrentDevice=current!=null&&entry.Serial==current.Serial};tile.Click+=(s,e)=>{var live=devices.Items.Cast<Device>().FirstOrDefault(x=>x.Serial==entry.Serial);if(live!=null)devices.SelectedItem=live;};deviceTiles.Controls.Add(tile);}if(devices.Items.Count==0)deviceTiles.Controls.Add(new Label{Text="暂无设备",AutoSize=true,ForeColor=Color.FromArgb(83,96,113),Margin=new Padding(4,20,0,0)});deviceTiles.ResumeLayout();}
  }
}
