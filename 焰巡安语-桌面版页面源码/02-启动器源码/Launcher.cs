using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace YanXunDesktop
{
    internal static class Program
    {
        private const int BASE_PORT = 8123;
        private const string APP_TITLE = "焰巡安语";
        private static string logPath = "";

        [STAThread]
        private static void Main(string[] args)
        {
            bool serveOnly = false;
            bool noEdge = false;
            int requestedPort = 0;
            foreach (string a in args)
            {
                if (a == "--serve-only") serveOnly = true;
                if (a == "--no-edge") noEdge = true;
                if (a.StartsWith("--port=", StringComparison.Ordinal))
                {
                    int.TryParse(a.Substring(7), out requestedPort);
                }
            }

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            logPath = Path.Combine(appDir, "launcher.log");
            Log("launcher start, args=" + string.Join(" ", args));
            string nodePath = Path.Combine(appDir, "node.exe");
            string serverPath = Path.Combine(appDir, "server", "weather-api.js");

            // 单实例：避免重复启动多个本地服务
            bool createdNew = true;
            Mutex mutex = null;
            if (!serveOnly)
            {
                mutex = new Mutex(true, "Global\\YanXunDesktop_1_0", out createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("焰巡安语已经在运行了。\n\n如果界面没出现，请先在任务栏找到它，或重启电脑。",
                        APP_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            Process nodeProc = null;
            bool startedNode = false;
            try
            {
                int port = requestedPort > 0 ? requestedPort : BASE_PORT;
                bool reuse = IsOurServer(port);
                Log("port=" + port + " reuse=" + reuse + " portOpen=" + PortOpen(port));

                if (!reuse)
                {
                    if (PortOpen(port)) port = FindFreePort(BASE_PORT + 1, BASE_PORT + 20);

                    if (!File.Exists(nodePath))
                    {
                        Fail("缺少 node.exe，请把整个「焰巡安语桌面版」文件夹一起复制，不要只拷 exe。");
                        return;
                    }
                    if (!File.Exists(serverPath))
                    {
                        Fail("缺少 server\\weather-api.js，请确认文件夹完整。");
                        return;
                    }

                    nodeProc = StartServer(nodePath, serverPath, appDir, port);
                    startedNode = true;
                    Log("node started pid=" + (nodeProc == null ? "null" : nodeProc.Id.ToString()));
                    bool okServer = nodeProc != null && WaitForServer(port, 25000);
                    Log("server ready=" + okServer);
                    if (!okServer)
                    {
                        Fail("本地服务启动失败，端口 " + port + "。\n\n" +
                             "可能原因：杀毒软件拦截 node.exe、端口被占用，或文件夹不完整。");
                        return;
                    }
                }

                string url = "http://127.0.0.1:" + port + "/";
                Log("url=" + url + " serveOnly=" + serveOnly);

                if (serveOnly)
                {
                    File.WriteAllText(Path.Combine(appDir, "serve.ready"), url);
                    while (true) Thread.Sleep(1000);
                }

                string edgePath = FindEdge();
                Log("edge=" + (edgePath == null ? "null" : edgePath));
                if (edgePath == null || noEdge)
                {
                    Process.Start(url);
                    MessageBox.Show("本地服务已启动：\n" + url +
                                    "\n\n如果浏览器没有自动打开，请手动复制上面的地址。\n" +
                                    "点击“确定”将停止本地服务并退出。",
                        APP_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string profileDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YanXunDesktop", "edge-profile");
                Directory.CreateDirectory(profileDir);

                string edgeArgs = "--app=" + url +
                                  " --user-data-dir=\"" + profileDir + "\"" +
                                  " --no-first-run --no-default-browser-check" +
                                  " --window-size=1280,860" +
                                  " --disable-features=msEdgeSidebarV2,msEdgeTakeover";

                Process edge = Process.Start(new ProcessStartInfo(edgePath, edgeArgs)
                {
                    UseShellExecute = false,
                    WorkingDirectory = appDir
                });

                if (edge == null)
                {
                    Process.Start(url);
                    MessageBox.Show("本地服务已启动：" + url, APP_TITLE);
                    return;
                }

                // 关掉应用窗口后，顺带停掉本地服务
                Log("edge started pid=" + edge.Id);
                edge.WaitForExit();
                Log("edge exited");
            }
            catch (Exception ex)
            {
                Fail("启动失败：" + ex.Message);
            }
            finally
            {
                if (startedNode && nodeProc != null)
                {
                    try { if (!nodeProc.HasExited) nodeProc.Kill(); }
                    catch { }
                }
                if (mutex != null)
                {
                    try { mutex.ReleaseMutex(); }
                    catch { }
                }
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss ") + message + "\r\n");
            }
            catch { }
        }

        private static void Fail(string message)
        {
            MessageBox.Show(message, APP_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static Process StartServer(string nodePath, string serverPath, string appDir, int port)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(nodePath, "\"" + serverPath + "\"");
                psi.WorkingDirectory = appDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.EnvironmentVariables["PORT"] = port.ToString();
                psi.EnvironmentVariables["NODE_ENV"] = "production";
                return Process.Start(psi);
            }
            catch
            {
                return null;
            }
        }

        private static bool PortOpen(int port)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(300)) return false;
                    c.EndConnect(ar);
                    return c.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>确认端口上跑的是本项目服务（裸 HTTP 请求，避免代理干扰）。</summary>
        private static bool IsOurServer(int port)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(500)) return false;
                    c.EndConnect(ar);
                    c.ReceiveTimeout = 900;
                    NetworkStream s = c.GetStream();
                    byte[] req = Encoding.ASCII.GetBytes(
                        "GET /api/health HTTP/1.0\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n");
                    s.Write(req, 0, req.Length);
                    byte[] buf = new byte[1024];
                    int n = s.Read(buf, 0, buf.Length);
                    if (n <= 0) return false;
                    string text = Encoding.ASCII.GetString(buf, 0, n);
                    return text.IndexOf("\"ok\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool WaitForServer(int port, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (IsOurServer(port)) return true;
                Thread.Sleep(300);
                waited += 300;
            }
            return false;
        }

        private static int FindFreePort(int from, int to)
        {
            for (int p = from; p <= to; p++)
            {
                if (!PortOpen(p)) return p;
            }
            return from;
        }

        private static string FindEdge()
        {
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] candidates = new string[]
            {
                Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Edge", "Application", "msedge.exe")
            };
            foreach (string p in candidates)
            {
                if (File.Exists(p)) return p;
            }
            return null;
        }
    }
}
