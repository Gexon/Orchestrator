using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

using EmpyrionModdingFramework;
using Eleon.Modding;
using System.Threading.Tasks;
using Eleon;
using System.Threading;

namespace Orchestrator
{
    public class DirectControl : EmpyrionModdingFrameworkBase
    {
        public string configFilePath;
        public Config Configuration = new Config();

        protected override void Initialize()
        {
            if (LegacyAPI == null)
            {
                ModAPI.Application.GameEntered += Application_GameEntered;
            }
            else
            {
                Game_EventRaised += PlayerConnected_Game_EventRaised;
            }


            CommandManager.CommandList.Add(new ChatCommand($"sping", (I) => ServerPing(I)));

        }

        // Это событие срабатывает только при запуске в режиме Dedi
        private void PlayerConnected_Game_EventRaised(CmdId eventId, ushort seqNr, object data)
        {
            if (eventId == CmdId.Event_Player_Connected)
            {
                PlayerInfo player = (PlayerInfo)data;
                Log($"Игрок {player.playerName} залогинился.");
            }
        }

        // Это событие существует только в клиентском режиме
        private void Application_GameEntered(bool hasEntered)
        {
            ShowGamePaths();

            configFilePath = ModAPI.Application.GetPathFor(AppFolder.SaveGame) + @"\Mods\" +
             ModName + @"\" + FrameworkConfig.ConfigFileName;
            try
            {
                using (StreamReader reader = File.OpenText(configFilePath))
                {
                    //Configuration = ConfigManager.LoadConfiguration<Config>(reader);
                }
            }
            catch (Exception error)
            {
                if (error is FileNotFoundException)
                {
                    try
                    {
                        GenerateEmptyConfig();
                    }
                    catch
                    {
                        throw;
                    }

                }
            }

            Log($"SaveGame: {ModAPI.Application.GetPathFor(AppFolder.SaveGame)}");
            Log($"Game Entered event {hasEntered}");
        }

        public void GenerateEmptyConfig()
        {
            Configuration.SenderNameOverride = "Gexon";
            using (StreamWriter writer = new StreamWriter(configFilePath))
            {
                ConfigManager.SaveConfiguration(writer, Configuration);
            }
        }

        private async Task ServerPing(MessageData data)
        {
            string msg;

            // Если мы находимся на выделенном сервере, используйте устаревший API и RequestManager, чтобы получить информацию для игрока
            if (LegacyAPI != null)
            {
                PlayerInfo player = (PlayerInfo)await RequestManager.SendGameRequest(CmdId.Request_Player_Info, new Id() { id = data.SenderEntityId });
                msg = $"{player.playerName} сказал {data.Text}";

            }
            // Если нет RequestManager, потому что мы не находимся на выделенном сервере, получите информацию от клиента
            else
            {
                IPlayer player = ModAPI.ClientPlayfield.Players[data.SenderEntityId];
                msg = $"{player.Name} сказал {data.Text}";
            }
            await SendFeedbackMessage(msg, data.SenderEntityId);
        }

        async Task SendFeedbackMessage(string msgText, int entityID)
        {
            await Task.Factory.StartNew(() => Thread.Sleep(500));

            var chatMsg = new MessageData()
            {
                SenderType = Eleon.SenderType.Player,
                Channel = Eleon.MsgChannel.SinglePlayer,
                RecipientEntityId = entityID,
                SenderNameOverride = Configuration.SenderNameOverride,
                Text = msgText
            };
            ModAPI.Application.SendChatMessage(chatMsg);
        }

        public void ShowGamePaths()
        {
            // Useful Application Paths - Check the logs
            Log($"SaveGame: {ModAPI.Application.GetPathFor(AppFolder.SaveGame)}");
            Log($"ActiveScenario: {ModAPI.Application.GetPathFor(AppFolder.ActiveScenario)}");
            Log($"Cache: {ModAPI.Application.GetPathFor(AppFolder.Cache)}");
            Log($"Content: {ModAPI.Application.GetPathFor(AppFolder.Content)}");
            Log($"Dedicated: {ModAPI.Application.GetPathFor(AppFolder.Dedicated)}");
            Log($"Mod: {ModAPI.Application.GetPathFor(AppFolder.Mod)}");
            Log($"Root: {ModAPI.Application.GetPathFor(AppFolder.Root)}");
        }

    }
}
