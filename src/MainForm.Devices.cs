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
    async Task RefreshDevicesFeedback(){if(refreshingDevices){deviceFeedback.Text="正在刷新设备列表……";return;}deviceFeedback.Text="正在刷新设备列表……";refreshDeviceButton.Enabled=false;try{await RefreshDevices();deviceFeedback.Text=lastDeviceRefreshSuccess?"设备列表已刷新，更新时间："+DateTime.Now.ToString("HH:mm:ss"):"刷新失败，请查看运行日志。";}finally{refreshDeviceButton.Enabled=true;}}
    async Task DeviceChanged(){int generation=++deviceGeneration;apps.Clear();FilterApps();selected.Text="请选择应用";var device=Current();RefreshHistory();if(device==null){appState.Text="连接手机后自动获取应用列表";UpdateState();return;}var cache=LoadCache(device.Identity);if(cache!=null&&cache.Items!=null){apps.AddRange(cache.Items);FilterApps();appState.Text=cache.Items.Count+" 个应用";status.Text="应用列表更新时间："+cache.Updated.ToString("yyyy/MM/dd HH:mm:ss")+"（缓存）";}else appState.Text=device.State=="device"?"准备获取应用列表……":"设备未连接";UpdateState();if(device.State=="device")await RefreshAppsFor(device,generation);}
    async Task RefreshDevices(){if(refreshingDevices)return;refreshingDevices=true;lastDeviceRefreshSuccess=false;try{var old=Current();string previous=old==null?"":old.Serial;string oldIdentity=old==null?"":old.Identity;int selectionGeneration=deviceGeneration;string output=await Run(cfg.Adb,"devices -l");var list=new List<Device>();foreach(string line in output.Split('\n')){var m=Regex.Match(line.Trim(),@"^(\S+)\s+(device|offline|unauthorized)\b(.*)$");if(m.Success){var model=Regex.Match(m.Groups[3].Value,@"model:(\S+)");list.Add(new Device{Serial=m.Groups[1].Value,State=m.Groups[2].Value,Model=model.Success?model.Groups[1].Value:"Android"});}}
      foreach(var item in list.Where(x=>x.State=="device")){try{string id=(await Run(cfg.Adb,"-s "+Quote(item.Serial)+" shell getprop ro.serialno",5000)).Trim();if(Regex.IsMatch(id,@"^[A-Za-z0-9._-]+$")&&id!="unknown")item.Identity=id;}catch(Exception ex){WriteLog("无法识别设备身份："+ex.Message);}}
      var distinct=new List<Device>();foreach(var item in list){var same=!String.IsNullOrEmpty(item.Identity)?distinct.FirstOrDefault(x=>x.Identity==item.Identity):null;if(same==null)distinct.Add(item);else if(item.Serial==previous){distinct.Remove(same);distinct.Add(item);}}
      if(IsDisposed||Disposing)return; if(selectionGeneration!=deviceGeneration){old=Current();previous=old==null?"":old.Serial;oldIdentity=old==null?"":old.Identity;}
      changingDevices=true;devices.BeginUpdate();devices.Items.Clear();foreach(var item in distinct)devices.Items.Add(item);int idx=distinct.FindIndex(x=>x.Serial==previous||(!String.IsNullOrEmpty(oldIdentity)&&x.Identity==oldIdentity));if(distinct.Count>0)devices.SelectedIndex=idx>=0?idx:0;devices.EndUpdate();changingDevices=false;var current=Current();bool changed=(old==null)!=(current==null)||(old!=null&&current!=null&&(old.Serial!=current.Serial||old.State!=current.State||old.Identity!=current.Identity));if(changed||old==null)status.Text=distinct.Count==0?"未发现设备。开启手机无线调试后，请点击「搜索并连接」。":"已发现 "+distinct.Count+" 台设备";if(changed)await DeviceChanged();else UpdateState();lastDeviceRefreshSuccess=true;
      }catch(Exception ex){changingDevices=false;status.Text="设备检查失败";WriteLog(ex.Message);}finally{refreshingDevices=false;UpdateState();}}
    async Task RefreshApps(){if(!Ready()||loadingApps)return;int generation=deviceGeneration;lastAppsRefreshSuccess=false;actionFeedback.Text="正在重新获取应用列表……";await RefreshAppsFor(Current(),generation);actionFeedback.Text=generation!=deviceGeneration?"设备已切换，请重新获取应用列表。":lastAppsRefreshSuccess?"应用列表已更新，"+DateTime.Now.ToString("HH:mm:ss"):"应用列表获取失败，请查看运行日志。";}
    async Task RefreshAppsFor(Device device,int generation){if(loadingApps)return;loadingApps=true;appState.Text=apps.Count>0?"正在更新缓存……":"正在获取应用列表……";UpdateState();try{string output=await Run(cfg.Scrcpy,"-s "+Quote(device.Serial)+" --list-apps",30000);if(generation!=deviceGeneration||Current()==null||Current().Serial!=device.Serial)return;var entries=new List<AppEntry>();foreach(string line in output.Split('\n')){var m=Regex.Match(line.Trim(),@"^[*-]\s+(.+?)\s{2,}([A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)\s*$");if(m.Success)entries.Add(new AppEntry{Name=m.Groups[1].Value.Trim(),Package=m.Groups[2].Value});}if(entries.Count==0){appState.Text="获取失败，保留已有缓存";WriteLog(output);return;}entries.Sort((x,y)=>String.Compare(x.Name,y.Name,StringComparison.CurrentCultureIgnoreCase));string selectedPackage=(appList.SelectedItem as AppEntry)==null?null:((AppEntry)appList.SelectedItem).Package;apps.Clear();apps.AddRange(entries);FilterApps();if(selectedPackage!=null){var match=appList.Items.Cast<AppEntry>().FirstOrDefault(x=>x.Package==selectedPackage);if(match!=null)appList.SelectedItem=match;}SaveCache(device.Identity,entries);appState.Text=entries.Count+" 个应用";status.Text="应用列表更新时间："+DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");lastAppsRefreshSuccess=true;}catch(Exception ex){if(generation==deviceGeneration){appState.Text=apps.Count>0?"更新失败，显示缓存":"获取失败，请重新获取";WriteLog("读取应用失败："+ex.Message);}}finally{loadingApps=false;UpdateState();if(!IsDisposed && !Disposing && IsHandleCreated && generation!=deviceGeneration && Current()!=null && Current().State=="device")BeginInvoke((Action)(async()=>await RefreshAppsFor(Current(),deviceGeneration)));}}
    void FilterApps(){string q=search.Text.Trim();appList.BeginUpdate();appList.Items.Clear();foreach(var x in apps.Where(x=>x.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||x.Package.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0))appList.Items.Add(x);appList.EndUpdate();RenderCards();}
    void RenderCards(){if(appGrid!=null)appGrid.SetItems(appList.Items.Cast<AppEntry>());}
    void UpdateCardSelection(){var chosen=appList.SelectedItem as AppEntry;if(appGrid!=null)appGrid.SetSelected(chosen);if(recentPanel!=null)foreach(Control control in recentPanel.Controls){var button=control as RecentButton;if(button==null)continue;button.Selected=chosen!=null&&(string)button.Tag==chosen.Package;button.Invalidate();}}
    static string Quote(string s){return "\""+s.Replace("\"","")+"\"";}
    class MdnsService { public string Name, Type, Address; public override string ToString(){return Name + "  " + Address;} }
    async Task<List<MdnsService>> Discover(string type) {
      string output=await Run(cfg.Adb,"mdns services",8000); var found=new List<MdnsService>();
      foreach(string line in output.Split('\n')) { var m=Regex.Match(line.Trim(),@"^(\S+)\s+(_adb-tls-(?:pairing|connect)\._tcp)\s+(\S+)$"); if(m.Success && m.Groups[2].Value==type && ValidAddress(m.Groups[3].Value)) found.Add(new MdnsService{Name=m.Groups[1].Value,Type=m.Groups[2].Value,Address=m.Groups[3].Value}); }
      return found;
    }
    Task PairDiscovery(){
      using(var f=new Form {Text="配对无线调试",ClientSize=new Size(590,320),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}) {
        var info=new Label{Left=15,Top=12,Width=555,Height=48,Text="在手机的无线调试页面选择「使用配对码配对设备」。\r\n发现配对服务后，选择设备并输入六位配对码。"};
        var list=new ListBox{Left=15,Top=65,Width=555,Height=95}; var detail=new Label{Left=15,Top=165,Width=555,Height=27,Text="正在搜索配对服务……"};
        var code=new TextBox{Left=15,Top=200,Width=180,MaxLength=6}; var go=new Button{Left=205,Top=197,Width=90,Height=30,Text="配对"};
        var manual=new Button{Left=305,Top=197,Width=120,Height=30,Text="手动输入地址"}; var close=new Button{Left=435,Top=197,Width=90,Height=30,Text="关闭"};
        var hint=new Label{Left=15,Top=242,Width=555,Height=60,Text="配对服务仅在手机配对码页面打开时出现。存在多个设备时，请选择对应地址。配对码不会保存。"};
        f.Controls.AddRange(new Control[]{info,list,detail,code,go,manual,close,hint});f.AcceptButton=go;close.Click+=(s,e)=>f.Close();
        bool scanning=false,working=false; var timer=new System.Windows.Forms.Timer{Interval=1800};
        Func<Task> scan=async()=>{ if(scanning||working||f.IsDisposed)return;scanning=true;try{var items=await Discover("_adb-tls-pairing._tcp");if(f.IsDisposed)return;string previous=(list.SelectedItem as MdnsService)==null?"":((MdnsService)list.SelectedItem).Address;list.BeginUpdate();list.Items.Clear();foreach(var item in items)list.Items.Add(item);int idx=items.FindIndex(x=>x.Address==previous);if(items.Count>0)list.SelectedIndex=idx>=0?idx:0;list.EndUpdate();detail.Text=items.Count==0?"未发现配对服务，请保持手机配对码页面打开。":"发现 "+items.Count+" 个配对服务";}catch(Exception ex){if(!f.IsDisposed)detail.Text="搜索失败："+ex.Message;}finally{scanning=false;}};
        timer.Tick+=async(s,e)=>await scan(); f.Shown+=async(s,e)=>{timer.Start();await scan();};f.FormClosed+=(s,e)=>timer.Stop();
        go.Click+=async(s,e)=>{if(working)return;var item=list.SelectedItem as MdnsService;if(item==null){detail.Text="请选择发现的配对服务，或手动输入地址。";return;}if(!Regex.IsMatch(code.Text.Trim(),@"^\d{6}$")){detail.Text="请输入六位配对码。";return;}working=true;go.Enabled=false;detail.Text="正在配对……";try{string output=await Run(cfg.Adb,"pair "+item.Address+" "+code.Text.Trim(),15000);if(f.IsDisposed)return;if(output.IndexOf("Successfully paired",StringComparison.OrdinalIgnoreCase)<0){detail.Text="配对失败，请检查配对码并保持手机配对页面打开。";WriteLog("配对失败："+output.Trim());}else{WriteLog("无线配对成功："+item.Address);code.Clear();detail.Text="配对成功。关闭窗口后，点击「搜索并连接」。";MessageBox.Show(f,"配对成功。关闭窗口后，点击「搜索并连接」。","配对成功");}}catch(Exception ex){if(!f.IsDisposed)detail.Text="配对失败："+ex.Message;}finally{working=false;if(!f.IsDisposed)go.Enabled=true;}};
        manual.Click+=async(s,e)=>{timer.Stop();f.Hide();await Pair();if(!f.IsDisposed)f.Close();};f.ShowDialog(this);timer.Dispose();
      }
      return Task.FromResult(0);
    }
    async Task StartWireless(){
      try{status.Text="正在发现无线调试服务……";await RefreshDevices();var all=devices.Items.Cast<Device>().Where(x=>x.State=="device" && (ValidAddress(x.Serial)||x.Serial.Contains("._adb-tls-connect._tcp"))).ToList();if(all.Count>0){if(Current()==null||!all.Contains(Current()))devices.SelectedItem=all[0];status.Text="无线 ADB 已连接，可获取应用列表。";return;}
        var services=await Discover("_adb-tls-connect._tcp");if(services.Count==0){status.Text="未发现无线调试服务，请检查手机设置或手动连接。";WriteLog(status.Text);return;}
        MdnsService chosen=services[0];if(services.Count>1){string address=Prompt("选择无线设备","发现多个设备，请输入其中一个地址：\r\n"+String.Join("  ",services.Select(x=>x.Address).ToArray()),chosen.Address);if(address==null)return;chosen=services.FirstOrDefault(x=>x.Address==address);if(chosen==null){MessageBox.Show("请输入已发现的设备地址。");return;}}
        string result=await Run(cfg.Adb,"connect "+chosen.Address,12000);WriteLog(result.Trim());await RefreshDevices();var target=devices.Items.Cast<Device>().FirstOrDefault(x=>x.State=="device"&&(x.Serial==chosen.Address||x.Serial.StartsWith(chosen.Name+".")));if(target!=null){devices.SelectedItem=target;status.Text="连接成功，可获取应用列表。";}else{status.Text="连接未完成，请检查配对状态或手动连接。";}
      }catch(Exception ex){status.Text="无线启动失败";WriteLog(ex.Message);}
    }
    async Task Connect(){string address=Prompt("无线连接","请输入手机无线调试主页面显示的 IP 地址和连接端口。",cfg.Address);if(address==null)return;if(!ValidAddress(address)){MessageBox.Show("请输入 IP 地址和端口，例如 192.0.2.1:5555。");return;}cfg.Address=address;SaveConfig();try{string result=await Run(cfg.Adb,"connect "+address,12000);WriteLog(result.Trim());await RefreshDevices();}catch(Exception ex){WriteLog("连接失败："+ex.Message);} }
    async Task Pair(){string address=Prompt("无线配对","请输入手机配对码页面显示的 IP 地址和配对端口。","");if(address==null)return;if(!ValidAddress(address)){MessageBox.Show("请输入有效的 IP 地址和端口。");return;}string code=Prompt("无线配对","请输入手机显示的六位配对码。","");if(code==null)return;if(!Regex.IsMatch(code,@"^\d{6}$")){MessageBox.Show("请输入六位数字配对码。");return;}try{string result=await Run(cfg.Adb,"pair "+address+" "+code,15000);WriteLog(result.Trim());MessageBox.Show("配对请求已完成。请返回手机的无线调试主页面，使用连接端口进行无线连接。\n\n"+result.Trim());}catch(Exception ex){WriteLog("配对失败："+ex.Message);} }
    static bool ValidAddress(string s){
      if(!Regex.IsMatch(s??"",@"^([0-9]{1,3}\.){3}[0-9]{1,3}:[0-9]{1,5}$"))return false;
      var parts=s.Split(':'); System.Net.IPAddress ip; int port;
      return System.Net.IPAddress.TryParse(parts[0],out ip) && ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork && Int32.TryParse(parts[1],out port) && port>=1 && port<=65535;
    }
    string Prompt(string title,string label,string value){using(var f=new Form{Text=title,ClientSize=new Size(440,125),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){var l=new Label{Text=label,Left=12,Top=12,Width=410};var t=new TextBox{Left=15,Top=38,Width=405,Text=value};var ok=new Button{Text="确定",Left=260,Top=75,DialogResult=DialogResult.OK};var cancel=new Button{Text="取消",Left=345,Top=75,DialogResult=DialogResult.Cancel};f.Controls.AddRange(new Control[]{l,t,ok,cancel});f.AcceptButton=ok;f.CancelButton=cancel;return f.ShowDialog(this)==DialogResult.OK?t.Text.Trim():null;}}
  }
}
