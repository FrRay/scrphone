using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PhoneLauncher {
  static class XmlStore {
    public static void Save<T>(string path, T value) {
      string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
      try {
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
          new XmlSerializer(typeof(T)).Serialize(stream, value);
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
      } finally {
        if (File.Exists(temp)) File.Delete(temp);
      }
    }
  }

  static class ProcessRunner {
    public static string Run(string file, string args, string adbPath, int timeout) {
      if (!File.Exists(file)) throw new FileNotFoundException("找不到程序：" + file, file);
      using (var process = new Process()) {
        process.StartInfo = new ProcessStartInfo(file, args) {
          UseShellExecute = false, CreateNoWindow = true,
          RedirectStandardOutput = true, RedirectStandardError = true,
          StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        process.StartInfo.EnvironmentVariables["ADB"] = adbPath;
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout)) {
          try { process.Kill(); } catch (InvalidOperationException) { }
          process.WaitForExit();
          throw new TimeoutException("操作超时");
        }
        Task.WaitAll(output, errors);
        string result = output.Result + errors.Result;
        if (process.ExitCode != 0)
          throw new InvalidOperationException(String.IsNullOrWhiteSpace(result) ? "进程退出码：" + process.ExitCode : result.Trim());
        return result;
      }
    }
  }
}
