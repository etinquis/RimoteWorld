﻿using NUnit.Framework;
using RimoteWorld.Core.API;
using RimoteWorld.Client;
using System;
using System.Diagnostics;
using System.Reflection;
using RimoteWorld.Client.API;
using RimoteWorld.Client.API.Remote;
using System.Linq;
using System.Threading;
using RimoteWorld.Core.Messaging.Instancing.UI;
using System.IO;
using System.Collections.Generic;
using System.Configuration;

namespace RimoteWorld.FullStack.Tests
{
    [TestFixture]
    public class FullStackTests
    {
        private Process RimWorldProcess = null;

        private static readonly DirectoryInfo RimWorldPath =
            new DirectoryInfo(ConfigurationSettings.AppSettings.Get("RimWorldPath"));
        private static readonly Version ExpectedRimWorldVersion =
            Version.Parse(ConfigurationSettings.AppSettings.Get("RimWorldVersion"));
        private static readonly string RimWorldExeName =
            ConfigurationSettings.AppSettings.Get("RimWorldExeName");

        private static readonly DirectoryInfo ServerModFolder =
            new DirectoryInfo(ConfigurationSettings.AppSettings.Get("ServerModPath"));
        private static readonly Version ExpectedRimoteWorldVersion =
            Assembly.GetAssembly(typeof(IServerAPI)).GetName().Version;

        private static readonly DirectoryInfo CCLModFolder =
            new DirectoryInfo(ConfigurationSettings.AppSettings.Get("CCLModPath"));
        private static readonly Version ExpectedCCLVersion =
            Version.Parse(ConfigurationSettings.AppSettings.Get("CCLVersion"));

        private DirectoryInfo ModsFolder;
        private DirectoryInfo StashedModsFolder;

        private IEnumerable<DirectoryInfo> NonCoreMods(DirectoryInfo modsFolder)
        {
            foreach (var folder in modsFolder.EnumerateDirectories())
            {
                if (!folder.Name.Equals("Core", StringComparison.InvariantCultureIgnoreCase))
                {
                    yield return folder;
                }
            }
        }

        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            if (!RimWorldPath.Exists)
            {
                Console.WriteLine(
                    "RimWorldPath is not valid. Set it to your RimWorld install path in RimWorld.config");
                Assert.Inconclusive();
            }

            var rimWorldReportedVersionFile = RimWorldPath.GetFiles().FirstOrDefault(f => f.Name.Equals("Version.txt"));
            if (rimWorldReportedVersionFile.Exists)
            {
                using (var fStream = new StreamReader(rimWorldReportedVersionFile.OpenRead()))
                {
                    var versionString = fStream.ReadLine();

                    Version reportedVersion;
                    if (Version.TryParse(versionString, out reportedVersion))
                    {
                        if (reportedVersion != ExpectedRimWorldVersion)
                        {
                            Console.WriteLine(
                                "Warning:: RimWorld Version.txt does not match ExpectedRimWorldVersion.  This might mean that ExpectedRimWorldVersion is not up-to-date.");
                        }
                    }
                }
            }



            ModsFolder = RimWorldPath.GetDirectories().FirstOrDefault(dir => dir.Name.Equals("Mods"));
            if (!ModsFolder.Exists)
            {
                Assert.Inconclusive("Mods folder can't be found beside RimWorldExe");
            }

            StashedModsFolder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            foreach (var mod in NonCoreMods(ModsFolder))
            {
                Console.WriteLine("Found non-core mod installed; stashing it to {0} to restore later",
                    StashedModsFolder.FullName);
                mod.MoveTo(Path.Combine(StashedModsFolder.FullName, mod.Name));
            }


            ServerModFolder.CopyTo(Path.Combine(ModsFolder.FullName, ServerModFolder.Name));
            CCLModFolder.CopyTo(Path.Combine(ModsFolder.FullName, CCLModFolder.Name));

            var os = Environment.OSVersion;
            FileInfo modsConfig = null;
            switch (os.Platform)
            {
                case PlatformID.MacOSX:
                {
                    modsConfig = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "RimWorld", "Config", "ModsConfig.xml"));
                    break;
                }
                case PlatformID.Unix:
                {
                    modsConfig =
                        new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".config", "unity3d", "Ludeon Studios", "RimWorld", "Config", "ModsConfig.xml"));
                    break;
                }
                default:
                {
                    modsConfig =
                        new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "AppData", "LocalLow", "Ludeon Studios", "RimWorld", "Config", "ModsConfig.xml"));
                    break;
                }
            }

            using (var writer = modsConfig.CreateText())
            {
                writer.WriteLine("<?xml version = \"1.0\" encoding = \"utf-8\"?>");
                writer.WriteLine("<ModsConfigData>");
                writer.WriteLine("<buildNumber>{0}</buildNumber>", ExpectedRimWorldVersion.Build);
                writer.WriteLine("<activeMods>");
                writer.WriteLine("<li>Core</li>");
                writer.WriteLine("<li>{0}</li>", CCLModFolder.Name);
                writer.WriteLine("<li>{0}</li>", ServerModFolder.Name);
                writer.WriteLine("</activeMods>");
                writer.WriteLine("</ModsConfigData>");
            }
        }

        [SetUp]
        public void SetUp()
        {
            var RimWorldExe =
                RimWorldPath.GetFiles()
                    .First(f => f.Name.StartsWith(RimWorldExeName, StringComparison.InvariantCultureIgnoreCase));
            RimWorldProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = RimWorldExe.FullName
                }
            };
            RimWorldProcess.Start();
            
            Thread.Sleep(TimeSpan.FromSeconds(10)); // startup time
        }

        [TearDown]
        public void TearDown()
        {
            if (!RimWorldProcess.HasExited)
            {
                RimWorldProcess.Kill();
            }
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            foreach (var mod in NonCoreMods(ModsFolder))
            {
                mod.Delete(true);
            }

            foreach (var mod in StashedModsFolder.EnumerateDirectories())
            {
                mod.MoveTo(Path.Combine(ModsFolder.FullName, mod.Name));
            }
        }

        [TestFixture]
        public class ServerAPITests : FullStackTests
        {
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
                var version = (Version)task.Result;
                Assert.That(version, Is.EqualTo(ExpectedRimWorldVersion));
            }

            [Test]
            public void GetRimoteWorldVersion()
            {
                var task = _remoteServerAPI.GetRimoteWorldVersion();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(300)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var version = (Version)task.Result;
                Assert.That(version, Is.EqualTo(ExpectedRimoteWorldVersion));
            }

            [Test]
            public void GetCCLVersion()
            {
                var task = _remoteServerAPI.GetCCLVersion();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(300)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var version = (Version)task.Result;
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
                Thread.Sleep(TimeSpan.FromSeconds(40));

                var task = _remoteMainMenuAPI.GetAvailableMainMenuOptions();
                Assert.That(task.Wait(TimeSpan.FromMilliseconds(500)), Is.True);
                Assert.That(() => task.Result, Throws.Nothing);
                var options = task.Result;

                var QuitToOs = options.First(opt => opt.MenuOptionText.Equals("Quit To OS", StringComparison.InvariantCultureIgnoreCase));
                var clickTask = _remoteMainMenuAPI.ClickMainMenuOption(QuitToOs);

                Assert.That(clickTask.Wait(TimeSpan.FromMilliseconds(600)), Is.True);
                Assert.That(() => clickTask.Wait(), Throws.Nothing);
                Assert.That(RimWorldProcess.WaitForExit((int) TimeSpan.FromSeconds(20).TotalMilliseconds), Is.True);
            }
        }
    }
}
