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
    async Task Launch(){if(busy||!Ready())return;var app=appList.SelectedItem as AppEntry;if(mode.SelectedIndex==0 && app==null){MessageBox.Show("请先选择应用。");return;}if(session!=null&&!session.HasExited){MessageBox.Show("请先结束当前投屏会话。");return;}int w=Number(width.Text,0),h=Number(height.Text,0),density=Number(dpi.Text,0);if(mode.SelectedIndex==0&&(w<100||w>8192||h<100||h>8192||density<72||density>1000)){MessageBox.Show("宽度和高度请输入 100～8192，DPI 请输入 72～1000。");return;}if(!File.Exists(cfg.Scrcpy)||!File.Exists(cfg.Adb)){MessageBox.Show("请先在工具路径中设置有效的 ADB 和 scrcpy。");return;}SaveConfig();var args=new List<string>{"-s",Quote(Current().Serial)};if(mode.SelectedIndex==0){args.Add("--new-display="+w+"x"+h+"/"+density);if(flex.Checked)args.Add("--flex-display");if(ime.Checked)args.Add("--display-ime-policy=local");}if(keep.Checked)args.Add("--keep-active");if(uhid.Checked)args.Add("--keyboard=uhid");if(mouse.SelectedIndex==0)args.Add("--mouse-bind=b---");else if(mouse.SelectedIndex==1)args.Add("--mouse-bind=----");if(app!=null)args.Add("--start-app="+app.Package);string serial=Current().Serial;string identity=Current().Identity;int historyRecorded=0;string command=String.Join(" ",args.ToArray());try{busy=true;if(session!=null){session.Dispose();session=null;}var p=new Process();p.StartInfo=new ProcessStartInfo(cfg.Scrcpy,command){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};p.StartInfo.EnvironmentVariables["ADB"]=cfg.Adb;p.EnableRaisingEvents=true;DataReceivedEventHandler onOutput=(s,e)=>{if(e.Data==null)return;WriteLog(e.Data);if(app!=null && e.Data.IndexOf("Starting app",StringComparison.OrdinalIgnoreCase)>=0 && System.Threading.Interlocked.Exchange(ref historyRecorded,1)==0){try{BeginInvoke((Action)(()=>RecordHistory(identity,app)));}catch{}}};p.OutputDataReceived+=onOutput;p.ErrorDataReceived+=onOutput;p.Exited+=(s,e)=>{WriteLog("投屏进程已结束，退出码："+p.ExitCode);if(!IsDisposed)try{BeginInvoke((Action)(()=>{status.Text="投屏已结束";actionFeedback.Text="投屏会话已结束（退出码 "+p.ExitCode+"）";UpdateState();}));}catch{}};WriteLog("启动参数："+command);p.Start();session=p;p.BeginOutputReadLine();p.BeginErrorReadLine();status.Text="正在启动投屏，请查看运行日志。";actionFeedback.Text="投屏已启动，可在此结束会话。";UpdateState();if(screenOff.Checked){await Task.Delay(2500);if(!p.HasExited){string result=await Run(cfg.Adb,"-s "+Quote(serial)+" shell cmd display power-off 0");WriteLog("已发送关闭主屏幕的请求。"+result.Trim());}}}catch(Exception ex){WriteLog("启动失败："+ex.Message);status.Text="启动失败";}finally{busy=false;UpdateState();} }
    void StopSession(){try{if(session!=null&&!session.HasExited){actionFeedback.Text="正在请求结束会话……";if(!session.CloseMainWindow()){actionFeedback.Text="无法请求关闭，请手动关闭投屏窗口。";WriteLog(actionFeedback.Text);}}else actionFeedback.Text="没有正在运行的会话。";}catch(Exception ex){actionFeedback.Text="结束会话失败："+ex.Message;WriteLog(actionFeedback.Text);} }
    async Task RestoreScreen(){if(!Ready())return;try{actionFeedback.Text="正在发送恢复屏幕请求……";string result=await Run(cfg.Adb,"-s "+Quote(Current().Serial)+" shell cmd display power-reset 0");if(result.IndexOf("unknown command",StringComparison.OrdinalIgnoreCase)>=0||result.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0){actionFeedback.Text="恢复屏幕失败："+result.Trim();}else actionFeedback.Text="已发送恢复手机屏幕的请求，请查看手机。";WriteLog(actionFeedback.Text);}catch(Exception ex){actionFeedback.Text="恢复屏幕失败："+ex.Message;WriteLog(actionFeedback.Text);} }
  }
}
