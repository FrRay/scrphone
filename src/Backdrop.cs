using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhoneLauncher {
  partial class MainForm {
    [StructLayout(LayoutKind.Sequential)]
    struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmExtendFrameIntoClientArea(IntPtr handle, ref Margins margins);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetLayeredWindowAttributes(IntPtr handle, int colorKey, byte alpha, int flags);

    // A separate child surface keeps GDI text opaque over the DWM backdrop.
    sealed class BackdropLayer : Panel {
      readonly Color key;
      public BackdropLayer(Color color) { key = color; BackColor = color; Dock = DockStyle.Fill; Margin = Padding.Empty; }
      protected override CreateParams CreateParams {
        get { var parameters = base.CreateParams; parameters.ExStyle |= 0x00080000; return parameters; }
      }
      protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        int colorRef = key.R | (key.G << 8) | (key.B << 16);
        if (!SetLayeredWindowAttributes(Handle, colorRef, 0, 1))
          throw new Win32Exception(Marshal.GetLastWin32Error());
      }
    }

    void ApplyWindowFrame(bool active) {
      if (!IsHandleCreated || IsDisposed) return;
      try {
        // Windows draws the border and shadow; unsupported attributes are ignored.
        int rounded = 2;
        DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
        int border = active ? 0x00C9BCAE : 0x00DED8D2; // COLORREF: BGR
        DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
      } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }

    void ApplyBackdrop() {
      BackdropLayer layer = null;
      try {
        // DWMSBT_MAINWINDOW is the Windows 11 Mica material.
        int backdrop = 2;
        if (DwmSetWindowAttribute(Handle, 38, ref backdrop, sizeof(int)) != 0) return;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        if (DwmExtendFrameIntoClientArea(Handle, ref margins) != 0) return;

        var key = Color.FromArgb(235, 238, 242);
        layer = new BackdropLayer(key);
        // Verify the child surface before changing the existing control hierarchy.
        Controls.Add(layer);
        var handle = layer.Handle;
        var children = new Control[Controls.Count - 1];
        int index = 0;
        foreach (Control child in Controls) if (child != layer) children[index++] = child;
        foreach (Control child in children) {
          Controls.Remove(child);
          layer.Controls.Add(child);
          SetBackdropKey(child, key);
        }
        BackColor = Color.Black;
        layer.BringToFront();
      } catch (DllNotFoundException) {
        if (layer != null) layer.Dispose();
      } catch (EntryPointNotFoundException) {
        if (layer != null) layer.Dispose();
      } catch (Win32Exception) {
        if (layer != null) layer.Dispose();
        var margins = new Margins();
        DwmExtendFrameIntoClientArea(Handle, ref margins);
      }
    }

    static void SetBackdropKey(Control control, Color key) {
      if (control is CaptionBar || control.BackColor.ToArgb() == Color.FromArgb(243, 247, 251).ToArgb())
        control.BackColor = key;
      foreach (Control child in control.Controls) SetBackdropKey(child, key);
    }
  }
}
