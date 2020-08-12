using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;

namespace Spider
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ModManager _modManager;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public Worker(ILogger<Worker> logger, IHostApplicationLifetime hostApplicationLifetime, ModManager modManager)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _modManager = modManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(100, stoppingToken);
            var gameVersion = Configuration.Current.EnabledGameVersions[0];
            await Configuration.InitializeConfigurationAsync("./config/spider.json");
            var addons = await _modManager.GetModInfoAsync(Configuration.Current.ModCount, gameVersion);
            List<Mod> existingMods;
            try
            {
                existingMods =
                    JsonSerializer.Deserialize<List<Mod>>(await File.ReadAllBytesAsync(Configuration.Current.ModInfoPath, stoppingToken));
            }
            catch (Exception e)
            {
                existingMods = new List<Mod>();
                _logger.LogError(e,"");
            }
            var mods = new List<Mod>();
            mods.AddRange(existingMods!);
            foreach (var addon in addons)
            {
                var modFile = addon.GameVersionLatestFiles.First(_ => _.GameVersion == gameVersion);
                var downloadUrl = ModHelper.JoinDownloadUrl(modFile.ProjectFileId.ToString(), modFile.ProjectFileName);
                var mod = new Mod
                {
                    Name = addon.Name,
                    ProjectId = addon.Id,
                    ProjectUrl = addon.WebsiteUrl,
                    DownloadUrl = downloadUrl,
                    LastCheckUpdateTime = DateTimeOffset.Now,
                    LastUpdateTime = addon.DateModified
                };
                if (ModHelper.ShouldPassMod(mod,existingMods))
                {
                    break;
                }
                mods.Add(mod);
            }
            _logger.LogInformation($"��api��ȡ��{mods.Count}��mod����Ϣ.");
            mods = await _modManager.DownloadModAsync(mods);
            mods = await _modManager.GetModIdAsync(mods);
            _logger.LogInformation($"����{mods.Count(_ => !string.IsNullOrEmpty(_.ModId))}��mod��modid.");
            await _modManager.SaveModInfoAsync(Configuration.Current.ModInfoPath, mods);
            _logger.LogInformation($"�洢������ {mods.Count} ��mod��Ϣ�� {Path.GetFullPath(Configuration.Current.ModInfoPath)} ");
            _logger.LogInformation("Exiting application...");
            _hostApplicationLifetime.StopApplication();
        }
    }
}
