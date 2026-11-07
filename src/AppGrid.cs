using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PhoneLauncher {
  class AppGrid : UserControl {
    readonly List<AppEntry> entries = new List<AppEntry>();
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    WebView2 browser;
    LegacyAppGrid fallback;
    string selectedPackage;
    bool initializing, ready, allowInitialNavigation=true; string pendingVisible;
    public event Action<AppEntry> ItemSelected;
    public event Action<AppEntry> ItemActivated;

    public AppGrid() { BackColor=Color.White; AccessibleName="全部应用"; }
    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      if (!initializing && browser==null && fallback==null) { initializing=true; BeginInvoke((Action)(async()=>await InitializeBrowser())); }
    }
    async Task InitializeBrowser() {
      try {
        browser=new WebView2 { Dock=DockStyle.Fill, DefaultBackgroundColor=Color.White };
        Controls.Add(browser);
        string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScrPhone","WebView2");
        var environment=await CoreWebView2Environment.CreateAsync(null,profile);
        await browser.EnsureCoreWebView2Async(environment);
        if(IsDisposed)return;
        using(var display=Graphics.FromHwnd(IntPtr.Zero))browser.ZoomFactor=96.0/display.DpiX;
        browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled=false;
        browser.CoreWebView2.NewWindowRequested+=(sender,args)=>args.Handled=true;
        browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
        browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
        browser.CoreWebView2.Settings.IsZoomControlEnabled=false;
        browser.CoreWebView2.WebMessageReceived+=OnWebMessage;
        browser.CoreWebView2.NavigationStarting+=(s,e)=>{if(allowInitialNavigation && e.Uri.StartsWith("data:text/html;charset=utf-8;base64,",StringComparison.Ordinal)){allowInitialNavigation=false;}else e.Cancel=true;};
        browser.NavigateToString(Page);
      } catch(Exception) {
        if(IsDisposed)return;
        if(browser!=null){Controls.Remove(browser);browser.Dispose();browser=null;}
        fallback=new LegacyAppGrid{Dock=DockStyle.Fill};
        fallback.ItemSelected+=item=>{if(ItemSelected!=null)ItemSelected(item);};
        fallback.ItemActivated+=item=>{if(ItemActivated!=null)ItemActivated(item);};
        Controls.Add(fallback);fallback.SetItems(entries);fallback.SetSelected(entries.FirstOrDefault(x=>x.Package==selectedPackage));
      }
    }
    void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
      if(e.Source!="about:blank")return;
      string message;try{message=e.TryGetWebMessageAsString();}catch(ArgumentException){return;}
      if(message=="ready"){ready=true;SendItems();if(pendingVisible!=null){Post(new {kind="visible",package=pendingVisible});pendingVisible=null;}return;}
      bool activate=message.StartsWith("activate:",StringComparison.Ordinal);
      if(!activate && !message.StartsWith("select:",StringComparison.Ordinal))return;
      string package=message.Substring(activate?9:7);
      var entry=entries.FirstOrDefault(x=>x.Package==package);if(entry==null)return;
      if(activate){if(ItemActivated!=null)ItemActivated(entry);}else if(ItemSelected!=null)ItemSelected(entry);
    }
    void Post(object payload){if(ready && browser!=null && !browser.IsDisposed)browser.CoreWebView2.PostWebMessageAsJson(json.Serialize(payload));}
    void SendItems(){Post(new {kind="items",items=entries.Select(x=>new {name=x.Name,package=x.Package}).ToArray(),selected=selectedPackage});}
    public void SetItems(IEnumerable<AppEntry> items){entries.Clear();entries.AddRange(items);if(fallback!=null)fallback.SetItems(entries);SendItems();}
    public void SetSelected(AppEntry entry){selectedPackage=entry==null?null:entry.Package;if(fallback!=null)fallback.SetSelected(entry);Post(new {kind="selected",selected=selectedPackage});}
    public void EnsureVisible(int index){if(index<0||index>=entries.Count)return;if(fallback!=null)fallback.EnsureVisible(index);if(!ready)pendingVisible=entries[index].Package;else Post(new {kind="visible",package=entries[index].Package});}

    const string Page=@"<!doctype html><html><head><meta charset='utf-8'><style>
html,body{margin:0;width:100%;height:100%;background:#fff;font:15px 'Microsoft YaHei UI',sans-serif;color:#1f242b;overflow:auto}
::-webkit-scrollbar{display:none} #grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:14px 12px;padding:0 4px 0 0}
button{box-sizing:border-box;height:60px;border:1px solid #cdd6e1;border-radius:10px;background:white;color:#1f242b;font:inherit;white-space:nowrap;text-overflow:ellipsis;overflow:hidden;padding:0 5px;cursor:pointer}
button:hover{background:#eaf3fc}button.selected{background:#dcedfc;border:2px solid #0067c0}button:focus{outline:none}button:focus-visible{outline:2px solid #0067c0;outline-offset:-4px}
</style></head><body><div id='grid' role='group' aria-label='全部应用'></div><script>
const grid=document.getElementById('grid'); let selected=null;
function sync(){for(const b of grid.children)b.classList.toggle('selected',b.dataset.package===selected)}
chrome.webview.addEventListener('message',event=>{const m=event.data;if(m.kind==='items'){selected=m.selected;grid.replaceChildren();for(const item of m.items){const b=document.createElement('button');b.type='button';b.textContent=item.name;b.title=item.name;b.dataset.package=item.package;grid.appendChild(b)}sync()}else if(m.kind==='selected'){selected=m.selected;sync()}else if(m.kind==='visible'){for(const b of grid.children)if(b.dataset.package===m.package){b.scrollIntoView({block:'nearest'});break}}});
grid.addEventListener('click',e=>{const b=e.target.closest('button');if(b)chrome.webview.postMessage('select:'+b.dataset.package)});
grid.addEventListener('dblclick',e=>{const b=e.target.closest('button');if(b)chrome.webview.postMessage('activate:'+b.dataset.package)});
grid.addEventListener('keydown',e=>{const b=e.target.closest('button');if(!b)return;const all=Array.from(grid.children);let i=all.indexOf(b);let columns=1;while(columns<all.length&&all[columns].offsetTop===all[0].offsetTop)columns++;if(e.key==='ArrowLeft')i--;else if(e.key==='ArrowRight')i++;else if(e.key==='ArrowUp')i-=columns;else if(e.key==='ArrowDown')i+=columns;else if(e.key==='Enter'){e.preventDefault();chrome.webview.postMessage('activate:'+b.dataset.package);return}else return;e.preventDefault();i=Math.max(0,Math.min(all.length-1,i));all[i].focus();chrome.webview.postMessage('select:'+all[i].dataset.package)});
chrome.webview.postMessage('ready');</script></body></html>";
  }
}
