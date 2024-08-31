using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VoiceAttackMinecraftPlugin
{
    public static class VoiceAttackPlugin
    {
        public static string VA_DisplayName() => "MinecraftVA Plugin";

        public static string VA_DisplayInfo() => "A VoiceAttack plugin for the MinecraftVA mod";

        public static Guid VA_Id() => new Guid("{ec1be0b9-eb99-4359-8fb7-41ba264e5672}");

        private static bool _stopVariableToMonitor = false;

        public static void VA_StopCommand()
        {
            _stopVariableToMonitor = true;
        }

        private const string HostName = "localhost";
        private const int Port = 25565;

        public static void VA_Init1(dynamic vaProxy)
        {
            vaProxy.WriteToLog("Minecraft VoiceAttack Plugin initializing...", "green");

            const int maxRetries = 3;
            const int retryDelayMs = 5000; // 5 seconds

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    vaProxy.WriteToLog($"Attempt {attempt}: Trying to connect to Minecraft mod...", "blue");
                    UpdateMappings(vaProxy);
                    vaProxy.WriteToLog("Minecraft VoiceAttack Plugin initialized successfully", "green");
                    return;
                }
                catch (Exception ex)
                {
                    vaProxy.WriteToLog($"Initialization attempt {attempt} failed: {ex.Message}", "yellow");
                    if (attempt < maxRetries)
                    {
                        vaProxy.WriteToLog($"Retrying in {retryDelayMs / 1000} seconds...", "yellow");
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                }
            }

            vaProxy.WriteToLog("Minecraft VoiceAttack Plugin initialization failed after maximum retries", "red");
        }

        public static void VA_Exit1(dynamic vaProxy)
        {
            vaProxy.WriteToLog("Minecraft VoiceAttack Plugin exited", "green");
        }

        public static void VA_Invoke1(dynamic vaProxy)
        {
            string context = vaProxy.Context;

            switch (context.ToLower())
            {
                case "update_mappings":
                    UpdateMappings(vaProxy);
                    break;
                case "execute_keybind":
                    string keybind = vaProxy.GetText("keybind");
                    ExecuteMethod(vaProxy, keybind);
                    break;
                case "create_voice_commands":
                    CreateVoiceCommands(vaProxy);
                    break;
                default:
                    vaProxy.WriteToLog($"Unknown context: {context}", "red");
                    break;
            }
        }

        private static void UpdateMappings(dynamic vaProxy)
        {
            try
            {
                vaProxy.WriteToLog("Preparing to send getMappings command to Minecraft mod...", "blue");
                string response = SendCommand(new JObject
                {
                    ["action"] = "getMappings"
                }, vaProxy);

                Dictionary<string, string> mappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);

                if (mappings == null || mappings.Count == 0)
                {
                    vaProxy.WriteToLog("Error: No mappings received or unable to deserialize JSON", "red");
                    return;
                }

                foreach (var mapping in mappings)
                {
                    vaProxy.SetText($"Minecraft.{mapping.Key}", mapping.Value);
                }

                vaProxy.WriteToLog($"Minecraft keybind mappings updated. Total mappings: {mappings.Count}", "green");
            }
            catch (Exception ex)
            {
                vaProxy.WriteToLog($"Error in UpdateMappings: {ex.GetType().Name} - {ex.Message}", "red");
                throw;
            }
        }

        private static void ExecuteMethod(dynamic vaProxy, string methodKey)
        {
            try
            {
                string response = SendCommand(new JObject
                {
                    ["action"] = "executeMethod",
                    ["translationKey"] = methodKey
                }, vaProxy);

                JObject result = JObject.Parse(response);

                if (result["success"].Value<bool>())
                {
                    vaProxy.WriteToLog($"Executed method for: {methodKey}", "green");
                }
                else
                {
                    vaProxy.WriteToLog($"Failed to execute method for: {methodKey}", "red");
                }
            }
            catch (Exception ex)
            {
                vaProxy.WriteToLog($"Error executing method: {ex.Message}", "red");
            }
        }

        private static void CreateVoiceCommands(dynamic vaProxy)
        {
            try
            {
                string response = SendCommand(new JObject
                {
                    ["action"] = "getMappings"
                }, vaProxy);

                Dictionary<string, string> mappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);

                if (mappings == null || mappings.Count == 0)
                {
                    vaProxy.WriteToLog("Error: No mappings received or unable to deserialize JSON", "red");
                    return;
                }

                int createdCommands = 0;
                List<string> createdCommandNames = new List<string>();

                foreach (var mapping in mappings)
                {
                    string commandName = $"Minecraft {mapping.Key.Replace("key.", "").Replace("gui.", "").Replace("_", " ")}";
                    string commandAction = $"[VoiceAttack.SetText(keybind, \"{mapping.Key}\")];" +
                                           "[VoiceAttack.ExecuteCommand(Execute Minecraft Method)]";

                    try
                    {
                        // Create or update the voice command
                        vaProxy.Command.Add(commandName, commandAction, "Minecraft");

                        createdCommandNames.Add(commandName);
                        createdCommands++;
                    }
                    catch (Exception cmdEx)
                    {
                        vaProxy.WriteToLog($"Error creating/updating command '{commandName}': {cmdEx.Message}", "red");
                    }
                }

                // Create or update the "Execute Minecraft Method" command
                string executeMethodCommand = "Execute Minecraft Method";
                string executeMethodAction = "[VoiceAttack.ExecuteExternalPlugin(MinecraftVA Plugin, execute_keybind, keybind)]";

                try
                {
                    vaProxy.Command.Add(executeMethodCommand, executeMethodAction, "Minecraft");
                    createdCommandNames.Add(executeMethodCommand);
                }
                catch (Exception cmdEx)
                {
                    vaProxy.WriteToLog($"Error creating/updating command '{executeMethodCommand}': {cmdEx.Message}", "red");
                }

                // Log the results
                foreach (string cmdName in createdCommandNames)
                {
                    vaProxy.WriteToLog($"Created/Updated command: {cmdName}", "green");
                }

                vaProxy.WriteToLog($"Voice commands created/updated. Total commands: {createdCommands}", "green");
            }
            catch (Exception ex)
            {
                vaProxy.WriteToLog($"Error creating voice commands: {ex.Message}", "red");
                vaProxy.WriteToLog($"Stack trace: {ex.StackTrace}", "red");
            }
        }

        private static string SendCommand(JObject command, dynamic vaProxy)
        {
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    vaProxy.WriteToLog("Attempting to connect to Minecraft mod...", "blue");
                    IAsyncResult ar = client.BeginConnect(HostName, Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5))) // 5-second timeout
                    {
                        throw new TimeoutException("Connection attempt timed out after 5 seconds");
                    }
                    client.EndConnect(ar);
                    vaProxy.WriteToLog("Connected to Minecraft mod", "green");

                    using (NetworkStream stream = client.GetStream())
                    {
                        vaProxy.WriteToLog("Sending command to Minecraft mod...", "blue");
                        byte[] data = Encoding.UTF8.GetBytes(command.ToString(Formatting.None) + "\n");
                        stream.Write(data, 0, data.Length);

                        vaProxy.WriteToLog("Waiting for response from Minecraft mod...", "blue");
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            string response = reader.ReadToEnd().Trim();
                            vaProxy.WriteToLog($"Received response: {response}", "green");
                            return response;
                        }
                    }
                }
                catch (Exception ex)
                {
                    vaProxy.WriteToLog($"Error in SendCommand: {ex.GetType().Name} - {ex.Message}", "red");
                    throw;
                }
            }
        }
    }
}