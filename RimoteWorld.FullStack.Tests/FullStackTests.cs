using NUnit.Framework;
using RimoteWorld.Core.API;
using RimoteWorld.Client;
using System;
using System.Diagnostics;
using System.Reflection;
using RimoteWorld.Client.API;
using RimoteWorld.Client.API.Remote;
using System.Linq;
using RimoteWorld.Core.Messaging.Instancing.UI;

namespace RimoteWorld.FullStack.Tests
{
    [TestFixture]
    public class FullStackTests
    {
        private Process RimWorldProcess = null;
        [SetUp]
        public void SetUp()
        {
            foreach (var runningProcess in Process.GetProcessesByName("RimWorldWin.exe"))
            {
                runningProcess.Kill();
            }

            RimWorldProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin.exe"
                }
            };
            RimWorldProcess.Start();
            
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10)); // startup time
        }

        [TearDown]
        public void TearDown()
        {
            if (!RimWorldProcess.HasExited)
            {
                RimWorldProcess.Kill();
            }
        }

        [TestFixture]
        public class ServerAPITests : FullStackTests
        {
            private static readonly Version ExpectedRimWorldVersion = new Version(0, 15, 1284, 139); // version.txt says build 134?
            private static readonly Version ExpectedRimoteWorldVersion = Assembly.GetAssembly(typeof(IServerAPI)).GetName().Version;
            private static readonly Version ExpectedCCLVersion = new Version(0, 15, 0);

            private IRemoteServerAPI _remoteServerAPI = null;

            [SetUp]
            public void SetUp()
            {
                _remoteServerAPI = new ClientAPI();
            }

            [TearDown]
            public void TearDown()
            {
                (_remoteServerAPI as ClientAPI).Shutdown();
            }

            [Test]
            public void GetRimWorldVersion()
            {
                var task = _remoteServerAPI.GetRimWorldVersion();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(300)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var version = (System.Version)task.Result;
                Assert.That(version, Is.EqualTo(ExpectedRimWorldVersion));
            }

            [Test]
            public void GetRimoteWorldVersion()
            {
                var task = _remoteServerAPI.GetRimoteWorldVersion();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(300)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var version = (System.Version)task.Result;
                Assert.That(version, Is.EqualTo(ExpectedRimoteWorldVersion));
            }

            [Test]
            public void GetCCLVersion()
            {
                var task = _remoteServerAPI.GetCCLVersion();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(300)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var version = (System.Version)task.Result;
                Assert.That(version, Is.EqualTo(ExpectedCCLVersion));
            }
        }

        [TestFixture]
        public class MainMenuAPITests : FullStackTests
        {
            private IRemoteMainMenuAPI _remoteMainMenuAPI = null;

            [SetUp]
            public void SetUp()
            {
                _remoteMainMenuAPI = new ClientAPI();
            }

            [TearDown]
            public void TearDown()
            {
                (_remoteMainMenuAPI as ClientAPI).Shutdown();
            }

            [Test]
            public void GetAvailableMainMenuOptions()
            {
                var task = _remoteMainMenuAPI.GetAvailableMainMenuOptions();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(300)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var options = task.Result;

                Assert.That(options.Length, Is.EqualTo(9));

                Func<string, MainMenuOptionLocator> getOption = (string label) =>
                {
                    return options.First(
                        opt => opt.MenuOptionText.Equals(label, StringComparison.InvariantCultureIgnoreCase));
                };

                Assert.That(() => getOption("Restart Now"), Throws.Nothing);
                Assert.That(() => getOption("Quick Start"), Throws.Nothing);
                Assert.That(() => getOption("New colony"), Throws.Nothing);
                Assert.That(() => getOption("Load game"), Throws.Nothing);
                Assert.That(() => getOption("Options"), Throws.Nothing);
                Assert.That(() => getOption("Mods"), Throws.Nothing);
                Assert.That(() => getOption("Mod Help"), Throws.Nothing);
                Assert.That(() => getOption("Credits"), Throws.Nothing);
                Assert.That(() => getOption("Quit To OS"), Throws.Nothing);
            }

            [Test]
            public void QuitToOs()
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(40));

                var task = _remoteMainMenuAPI.GetAvailableMainMenuOptions();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(500)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var options = task.Result;

                var QuitToOs = options.First(opt => opt.MenuOptionText.Equals("Quit To OS", StringComparison.InvariantCultureIgnoreCase));
                var clickTask = _remoteMainMenuAPI.ClickMainMenuOption(QuitToOs);

                Assert.That(clickTask.Wait(TimeSpan.FromMilliseconds(500)), Is.True);
                Assert.That(() => clickTask.Wait(), Throws.Nothing);
                Assert.That(RimWorldProcess.WaitForExit((int) TimeSpan.FromSeconds(20).TotalMilliseconds), Is.True);
            }
        }
    }
}
