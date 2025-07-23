using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace QweenIris
{
    internal class Channels
    {
        public Channels(IConfigurationRoot config)
        {
            CharacterCardChannelID = config["Discord:CharacterCardChannelID"] == null ? 0UL : ulong.Parse(config["Discord:CharacterCardChannelID"]);
            InstructionsChannelID = config["Discord:InstructionsChannelID"] == null ? 0UL : ulong.Parse(config["Discord:InstructionsChannelID"]);
            CodeInstructionsChannelID = config["Discord:CodeInstructionsChannelID"] == null ? 0UL : ulong.Parse(config["Discord:CodeInstructionsChannelID"]);
            NewsInstructionsChannelID = config["Discord:NewsInstructionsChannelID"] == null ? 0UL : ulong.Parse(config["Discord:NewsInstructionsChannelID"]);
            WatchedChannelID = config["Discord:WatchedChannelID"] == null ? 0UL : ulong.Parse(config["Discord:WatchedChannelID"]);
        }

        internal ulong CharacterCardChannelID;
        internal ulong InstructionsChannelID;
        internal ulong CodeInstructionsChannelID;
        internal ulong NewsInstructionsChannelID;
        internal ulong WatchedChannelID;
    }

    internal class DiscordBot
    {
        private readonly DiscordSocketClient client;
        private readonly string token;
        private readonly ulong chatChannelID;
        List<RestUserMessage> messagesToDeleteIfOverriden;

        private async Task<string> GetLastMessagesOnChannel(ulong channelID, int depth = 1)
        {
            var channel = client.GetChannel(channelID) as SocketTextChannel;
            var message = "";
            var instructionChannelMessages = await channel.GetMessagesAsync(limit: depth).FlattenAsync();
            foreach (var instructionMessage in instructionChannelMessages)
            {
                message += " " + instructionMessage.Content;
            }

            return message;
        }

        public DiscordBot(Action<PromptContext> action)
        {
            this.client = new DiscordSocketClient();
            messagesToDeleteIfOverriden = new List<RestUserMessage>();
            var config = Config.Load();
            token = config["Discord:Token"];
            var botTrigger = config["Discord:BotTrigger"];
            var instructionChannels = new Channels(config);
            chatChannelID = instructionChannels.WatchedChannelID;
            this.client.MessageReceived += async message =>
            {
                // Avoid blocking the gateway task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var channel = client.GetChannel(message.Channel.Id) as SocketTextChannel;
                        var messages = await channel.GetMessagesAsync(limit: 1).FlattenAsync();
                        var isMentionningBot = false;
                        if (message.Content.Contains(botTrigger))
                            isMentionningBot = true;
                        if ((message.Channel.Id != instructionChannels.WatchedChannelID || message.Author.IsBot))
                            return;
                        messagesToDeleteIfOverriden.Clear();
                        Console.WriteLine("New message received:");
                        var CharacterInstruction = await GetLastMessagesOnChannel(instructionChannels.InstructionsChannelID);
                        var characterIDInstruction = await GetLastMessagesOnChannel(instructionChannels.CharacterCardChannelID);
                        var codeInstruction = await GetLastMessagesOnChannel(instructionChannels.CodeInstructionsChannelID);
                        var newsInstruction = await GetLastMessagesOnChannel(instructionChannels.NewsInstructionsChannelID);

                        var history = await channel.GetMessagesAsync(limit: 10).FlattenAsync();
                        var parsedHistory = "";

                        var currentPastMessageLooked = 0;
                        foreach (var pastMessage in history)
                        {
                            if (currentPastMessageLooked > 0)
                            {
                                parsedHistory += $"from: {pastMessage.Author} Message:{pastMessage.Content}\n";
                            }
                            currentPastMessageLooked++;
                        }

                        var shortHistory = await channel.GetMessagesAsync(limit: 5).FlattenAsync();
                        var shortParsedHistory = "";

                        var shortCurrentPastMessageLooked = 0;
                        foreach (var pastMessage in shortHistory)
                        {
                            if (currentPastMessageLooked > 0)
                            {
                                shortParsedHistory += $"from: {pastMessage.Author} Message:{pastMessage.Content}\n";
                            }
                            shortCurrentPastMessageLooked++;
                        }

                        var messageLooked = 0;
                        foreach (var message in messages)
                        {
                            var promptContext = new PromptContext();
                            promptContext.SetInstructions(characterIDInstruction, CharacterInstruction, codeInstruction, newsInstruction);
                            promptContext.SetUser(message.Author.Username);
                            promptContext.SetHistory(parsedHistory, shortParsedHistory);
                            promptContext.SetPrompt(message.Content);
                            action.Invoke(promptContext);
                        }


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in MessageHandler: {ex.Message}");
                    }
                });
            };
        }

        public async Task DeleteLastOverrideBotMessage()
        {
            await Task.Delay(500); // Waits for 1 second (1000 milliseconds)

            try
            {
                foreach (var overrideMessage in messagesToDeleteIfOverriden)
                {
                    var channel = client.GetChannel(overrideMessage.Channel.Id) as SocketTextChannel;
                    var message = await channel.GetMessageAsync(overrideMessage.Id);
                    await message.DeleteAsync();
                }
            } catch
            {
                ReplyAsync("Ooops couldn't delete messages", false);
            }
            messagesToDeleteIfOverriden.Clear();


        }

        public async Task ReplyAsync(string response, bool deleteIfOveridden, bool isStackedMessage = false)
        {
            var message = client.GetChannel(chatChannelID) as SocketTextChannel;
            if(!isStackedMessage)
            {
                await DeleteLastOverrideBotMessage();
            }

            if (response.Length > 2000)
            {
                string[] splitBlocks = response.Split(new string[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var block in splitBlocks)
                {
                    string [] lines = { block };
                    if (block.Length > 2000)
                    {
                        lines = block.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    foreach (var line in lines)
                    {
                        await ReplyAsync(line, deleteIfOveridden, true);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(response) && response.Length > 0)
            {
                response = response.Replace("---", "");
                var messageOnServer = await message.SendMessageAsync(response);
                if (deleteIfOveridden)
                {
                    messagesToDeleteIfOverriden.Add(messageOnServer);
                }
            }

        }

        DateTime lastPingReceived;

        public async Task TriggerTyping()
        {
            var triggerPing = false;
            if (lastPingReceived == null)
            {
                triggerPing = true;
                lastPingReceived = DateTime.Now;
            }


            TimeSpan elapsed = DateTime.Now - lastPingReceived;
            double seconds = elapsed.TotalSeconds;

            if (elapsed.Seconds > 3)
            {
                lastPingReceived = DateTime.Now;
                triggerPing = true;
            }

            if (triggerPing)
            {
                Console.WriteLine("Trigger typing");
                var channel = client.GetChannel(chatChannelID) as SocketTextChannel;
                await channel.TriggerTypingAsync();
            }
        }
        public async Task StartAsync()
        {
            this.client.Log += LogFuncAsync;

            await this.client.LoginAsync(TokenType.Bot, token);
            await this.client.StartAsync();
            await Task.Delay(-1);

            async Task LogFuncAsync(LogMessage message) =>
                Console.Write(message.ToString());
        }

    }
}
