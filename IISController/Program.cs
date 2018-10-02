using System;
using System.ServiceProcess;
using System.DirectoryServices;
using Microsoft.Web.Administration;
using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;
using System.Diagnostics;

namespace IISController
{
    class Program
    {
        private static ServerManager sm = new ServerManager();
        private static string ViewerName = "Viewer";
        private static string ServicesName = "Services";

        static void Main(string[] args)
        {
            //step1 检测IIS
            Console.WriteLine("开始检测IIS");
            CheckIIS();

            //step2 判断程序池是否存在
            ShowMessage("开始检测Viewer程序池");
            if (IsAppPoolName(ViewerName))
            {
                ShowMessage(ViewerName + " 程序池已存在，请尝试其他名称:");
                while (IsAppPoolName(ViewerName = Console.ReadLine()))
                {
                    ShowMessage(ViewerName + " 此程序池已存在，请尝试其他名称:");
                }
            }

            ShowMessage("开始检测Services程序池");
            if (IsAppPoolName(ServicesName))
            {
                ShowMessage(ServicesName + " 程序池已存在，请尝试其他名称:");
                while (IsAppPoolName(ServicesName = Console.ReadLine()))
                {
                    ShowMessage(ServicesName + " 程序池已存在，请尝试其他名称:");
                }
            }

            //step3 创建独立程序池
            ShowMessage("开始创建Viewer程序池");
            CreateAppPool("/Viewer", ViewerName);

            ShowMessage("开始创建Services程序池");
            CreateAppPool("/Services", ServicesName);

            //step4 配置站点文件夹权限
            ShowMessage("开始配置站点文件夹IIS_USRS权限");
            SetFileAccessRule();

            //step5 启动aspnet_regiis.exe
            ShowMessage("开始启动aspnet_regiis，请稍候");
            StartAspnetRegiis();

            ShowMessage("IIS配置完成，请按任意键结束");
            Console.ReadKey();

            //独立编译此方法，修改DefaultAppPool支持32位，以支持Install配置IIS站点绑定AppPool
            //SetDefaultPoolToEnable32Bit();
        }

        /// <summary>
        /// 检测IIS，获取版本
        /// </summary>
        private static void CheckIIS()
        {
            try
            {
                bool isExist = false;
                ServiceController[] services = ServiceController.GetServices();
                foreach (var service in services)
                {
                    if (service.ServiceName.Equals("W3SVC"))
                    {
                        isExist = true;
                        break;
                    }
                }
                if (!isExist)
                {
                    ShowErrorAndExit("服务器未开启IIS功能");
                }

                DirectoryEntry getEntry = new DirectoryEntry("IIS://localhost/W3SVC/INFO");
                string version = getEntry.Properties["MajorIISVersionNumber"].Value.ToString();
                ShowMessage("IIS Version:" + version);
            }
            catch (Exception ex)
            {
                ShowErrorAndExit(ex.ToString());
            }
        }

        /// <summary>
        /// 判断程序池是否存在
        /// </summary>
        /// <param name="AppName">程序池名称</param>
        /// <returns>true存在 false不存在</returns>
        private static bool IsAppPoolName(string PoolName)
        {
            if (string.IsNullOrWhiteSpace(PoolName))
            {
                return false;
            }

            foreach (var poor in sm.ApplicationPools)
            {
                if (poor.Name.Equals(PoolName))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 创建程序池
        /// </summary>
        /// <param name="AppPath"></param>
        /// <param name="PoolName"></param>
        private static void CreateAppPool(string AppPath, string PoolName)
        {
            try
            {
                var newPool = sm.ApplicationPools.Add(PoolName);
                newPool.ManagedRuntimeVersion = "v4.0";
                newPool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
                newPool.Enable32BitAppOnWin64 = true;
                sm.CommitChanges();
                ShowMessage(PoolName + "程序池托管管道模式为:" + ManagedPipelineMode.Integrated.ToString() + "，运行的NET版本为:v4.0");

                sm.Sites["Viewer"].Applications[AppPath].ApplicationPoolName = PoolName;
                sm.CommitChanges();
            }
            catch (Exception ex)
            {
                ShowErrorAndExit(ex.ToString());
            }
        }

        /// <summary>
        /// 配置Site文件夹IIS_IUSRS权限
        /// </summary>
        private static void SetFileAccessRule()
        {
            try
            {
                DirectorySecurity ds = new DirectorySecurity();
                ds.AddAccessRule(new FileSystemAccessRule("IIS_IUSRS", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                Directory.SetAccessControl("C:/inetpub/wwwroot/viewer", ds);
            }
            catch (Exception ex)
            {
                ShowErrorAndExit(ex.ToString());
            }
        }

        /// <summary>
        /// 启动aspnet_regiis.exe
        /// </summary>
        private static void StartAspnetRegiis()
        {
            string fileName_regiis = Environment.GetEnvironmentVariable("windir") + @"\Microsoft.NET\Framework\v4.0.30319\aspnet_regiis.exe";
            ProcessStartInfo startInfo = new ProcessStartInfo(fileName_regiis);
            startInfo.Arguments = "-i";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            string errors = process.StandardError.ReadToEnd();
            if (errors != string.Empty)
            {
                ShowErrorAndExit(errors);
            }
        }

        /// <summary>
        /// 初始化默认Pool支持32位
        /// </summary>
        private static void SetDefaultPoolToEnable32Bit()
        {
            sm.ApplicationPools["DefaultAppPool"].Enable32BitAppOnWin64 = true;
            sm.CommitChanges();
        }

        private static void ShowErrorAndExit(string message)
        {
            ShowMessage(message);
            Console.WriteLine("请按任意键退出");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void ShowMessage(string message)
        {
            Console.WriteLine();
            Console.WriteLine("Message:" + message);
        }
    }
}
