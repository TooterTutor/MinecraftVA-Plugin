using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
        private const int DefaultPort = 28463;

        private static int Port; // Store the current port

        public static void VA_Init1(dynamic vaProxy)
        {
            vaProxy.WriteToLog("Minecraft VoiceAttack Plugin initializing...", "green");

            // Start handshake to get the port
            Task.Run(() => Handshake(vaProxy)).Wait();
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

        private static string SendCommand(JObject command, dynamic vaProxy)
        {
            try
            {
                return SendCommandInternal(command, vaProxy);
            }
            catch (Exception ex)
            {
                vaProxy.WriteToLog($"Error in SendCommand: {ex.GetType().Name} - {ex.Message}", "red");
                vaProxy.WriteToLog("Attempting to reconnect using handshake...", "yellow");

                // Attempt to reconnect using handshake
                Task.Run(() => Handshake(vaProxy)).Wait();

                // Retry sending the command
                try
                {
                    return SendCommandInternal(command, vaProxy);
                }
                catch (Exception retryEx)
                {
                    vaProxy.WriteToLog($"Error in SendCommand after reconnect: {retryEx.GetType().Name} - {retryEx.Message}", "red");
                    throw;
                }
            }
        }

        private static string SendCommandInternal(JObject command, dynamic vaProxy)
        {
            using (TcpClient client = new TcpClient())
            {
                vaProxy.WriteToLog("Attempting to connect to Minecraft mod...", "blue");
                IAsyncResult ar = client.BeginConnect(HostName, Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5))) // 5-second timeout
                {
                    throw new TimeoutException("Connection attempt timed out after 5 seconds");
                }
                client.EndConnect(ar);
                //vaProxy.WriteToLog("Connected to Minecraft mod", "green");

                using (NetworkStream stream = client.GetStream())
                {
                    //vaProxy.WriteToLog("Sending command to Minecraft mod...", "blue");
                    byte[] data = Encoding.UTF8.GetBytes(command.ToString(Formatting.None) + "\n");
                    stream.Write(data, 0, data.Length);

                    //vaProxy.WriteToLog("Waiting for response from Minecraft mod...", "blue");
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string response = reader.ReadToEnd().Trim();
                        //vaProxy.WriteToLog($"Received response: {response}", "green");
                        return response;
                    }
                }
            }
        }

        private static async Task Handshake(dynamic vaProxy)
        {
            int retryCount = 0;
            const int maxRetries = 5;
            const int retryDelay = 5000; // 5 seconds

            while (retryCount < maxRetries)
            {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        //vaProxy.WriteToLog("Sending handshake to Minecraft mod...", "blue");
                        await client.ConnectAsync(HostName, DefaultPort);
                        using (NetworkStream stream = client.GetStream())
                        using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false))) // Disable BOM
                        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true }) // Disable BOM
                        {
                            JObject handshakeCommand = new JObject
                            {
                                ["command"] = "handshake"
                            };
                            await writer.WriteLineAsync(handshakeCommand.ToString(Formatting.None));
                            //vaProxy.WriteToLog("Handshake command sent, waiting for response...", "blue");

                            string response = await reader.ReadLineAsync();
                            //vaProxy.WriteToLog($"Raw response from handshake: {response}", "blue");

                            if (string.IsNullOrEmpty(response))
                            {
                                throw new Exception("Handshake response is empty");
                            }

                            JObject jsonResponse = JObject.Parse(response);
                            if (!jsonResponse.ContainsKey("port"))
                            {
                                throw new Exception("Handshake response does not contain 'port'");
                            }

                            Port = jsonResponse["port"].Value<int>();
                            vaProxy.WriteToLog($"Received port from handshake: {Port}", "green");
                            return; // Exit the method after successfully receiving the port
                        }
                    }
                }
                catch (Exception ex)
                {
                    vaProxy.WriteToLog($"Error during handshake: {ex.Message}", "red");
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        vaProxy.WriteToLog($"Retrying handshake... Attempt {retryCount}/{maxRetries}", "yellow");
                        await Task.Delay(retryDelay);
                    }
                    else
                    {
                        vaProxy.WriteToLog("Max retries reached. Failed to receive port acknowledgment.", "red");
                    }
                }
            }
        }
    }
}
