using DarkRift;
using DarkRift.Client;
using DarkRift.Client.Unity;
using DVMultiplayer.Utils;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DVMultiplayer.Networking
{
    public static class NetworkManager
    {
        public static UnityClient client;
        private static NetworkingUI UI;
        private static GameObject networkManager;
        private static bool _isHost;
        private static bool isClient;
        private static bool isConnecting;
        private static string host;
        private static int port;
        internal static string username;
        private static bool scriptsInitialized = false;
        private static int tries = 1;
        private static PlayerDistanceMultipleGameObjectsOptimizer[] objectDisablers;

        /// <summary>
        /// Initializes the NetworkManager by:
        /// Spawning all the components needed.
        /// Listening to events.
        /// </summary>
        public static void Initialize()
        {
            Main.Log("Initializing NetworkManager");
            _isHost = false;
            isClient = false;
            if (!UGameObject.Exists("NetworkManager"))
            {
                networkManager = Object.Instantiate(new GameObject(), Vector3.zero, Quaternion.identity);
                networkManager.name = "NetworkManager";
                client = networkManager.AddComponent<UnityClient>();

                client.Disconnected += OnClientDisconnected;

                Object.DontDestroyOnLoad(networkManager);

            }

            if (UI == null)
            {
                UI = new NetworkingUI();
                UI.Setup();
            }
        }

        private static void OnClientDisconnected(object sender, DisconnectedEventArgs e)
        {
            SingletonBehaviour<CoroutineManager>.Instance.Run(DisconnectCoroutine());
        }

        private static IEnumerator DisconnectCoroutine()
        {
            yield return new WaitUntil(() => !CustomUI.currentScreen);
            yield return new WaitForEndOfFrame();
            if (scriptsInitialized)
            {
                yield return DeInitializeUnityScripts();
                scriptsInitialized = false;
            }
            foreach (PlayerDistanceMultipleGameObjectsOptimizer disabler in objectDisablers)
            {
                disabler.enabled = true;
            }
            isClient = false;
            client.Close();
        }

        /// <summary>
        /// Deinitializing by destroying this gameobject.
        /// </summary>
        public static void Deinitialize()
        {
            Main.Log("Deinitializing NetworkManager");

            if (UGameObject.Exists("NetworkManager"))
            {
                GameObject.Destroy(GameObject.Find("NetworkManager"));
            }
        }

        /// <summary>
        /// Connects to the server with a given host and port
        /// </summary>
        /// <param name="host">The hostname to connect to</param>
        /// <param name="port">The port of the server</param>
        public static void Connect(string host, int port, string username)
        {
            NetworkManager.username = username;
            NetworkManager.host = host;
            NetworkManager.port = port;
            ClientConnect();
        }

        private static void ClientConnect()
        {
            if (isClient || isConnecting)
                return;

            isConnecting = true;
            Main.Log("[CLIENT] Connecting to server");
            client.ConnectInBackground(host, port, true, OnConnected);
        }

        /// <summary>
        /// Disconnects the client from the server.
        /// </summary>
        public static void Disconnect()
        {
            if (!isClient)
                return;

            Main.Log($"Disconnecting client");
            try
            {
                client.Disconnect();
            }
            catch (Exception ex)
            {
                Main.Log($"[ERROR] {ex.InnerException}");
            }
        }

        /// <summary>
        /// Starts up the game server and connects to it automatically
        /// </summary>
        


        /// <summary>
        /// Stops the hosted server and disconnects from it
        /// </summary>
        

        private static void OnConnected(Exception ex)
        {
            isConnecting = false;
            if (ex != null && !string.IsNullOrEmpty(ex.Message))
            {
                isClient = false;
                Main.Log($"[ERROR] {ex.Message}");
                Main.mod.Logger.Log($"[CLIENT] Connecting failed retrying..., tries: {tries}/5");
                if (tries < 5)
                {
                    tries++;
                    ClientConnect();
                }
                else
                {
                    Main.mod.Logger.Log($"[CLIENT] Connecting failed stopping retries.");
                    tries = 1;
                }
            }
            else
            {
                isClient = true;
                UI.HideUI();
                Main.Log($"Disabling autosave");
                SingletonBehaviour<SaveGameManager>.Instance.disableAutosave = true;
                CarSpawner.useCarPooling = false;
                if (!scriptsInitialized)
                {
                    Main.Log($"Client connected loading required unity scripts");
                    InitializeUnityScripts(); 
                    scriptsInitialized = true;
                }

                objectDisablers = GameObject.FindObjectsOfType<PlayerDistanceMultipleGameObjectsOptimizer>();
                foreach(PlayerDistanceMultipleGameObjectsOptimizer disabler in objectDisablers)
                {
                    disabler.enabled = false;
                }

                Main.Log($"Everything should be initialized running PlayerConnect method");
                SingletonBehaviour<NetworkPlayerManager>.Instance.PlayerConnect();
                Main.Log($"Connecting finished");
            }
        }

        internal static void SetIsHost(bool isHost)
        {
            Main.Log("[CLIENT] Set Role "+(isHost ? "HOST":"CLIENT"));
            _isHost = isHost;
        }

        private static void InitializeUnityScripts()
        {
            Main.Log($"[CLIENT] Initializing Player");
            NetworkPlayerSync playerSync = PlayerManager.PlayerTransform.gameObject.AddComponent<NetworkPlayerSync>();
            playerSync.IsLocal = true;
            playerSync.Username = username;
            playerSync.Id = client.ID;

            Main.Log($"[CLIENT] Initializing NetworkPlayerManager");
            networkManager.AddComponent<NetworkPlayerManager>();
            Main.Log($"[CLIENT] Initializing NetworkTrainManager");
            networkManager.AddComponent<NetworkTrainManager>();
            Main.Log($"[CLIENT] Initializing NetworkJunctionManager");
            networkManager.AddComponent<NetworkJunctionManager>();
            Main.Log($"[CLIENT] Initializing NetworkSaveGameManager");
            networkManager.AddComponent<NetworkSaveGameManager>();
            Main.Log($"[CLIENT] Initializing NetworkJobsManager");
            networkManager.AddComponent<NetworkJobsManager>();
            Main.Log($"[CLIENT] Initializing NetworkTurntableManager");
            networkManager.AddComponent<NetworkTurntableManager>();
            Main.Log($"[CLIENT] Initializing NetworkDebtManager");
            networkManager.AddComponent<NetworkDebtManager>();
        }

        private static IEnumerator DeInitializeUnityScripts()
        {
            Main.Log($"[DISCONNECTING] NetworkPlayerSync Deinitializing");
            Object.DestroyImmediate(PlayerManager.PlayerTransform.GetComponent<NetworkPlayerSync>());
            Main.Log($"[DISCONNECTING] NetworkPlayerManager Deinitializing");
            Object.DestroyImmediate(networkManager.GetComponent<NetworkPlayerManager>());
            Main.Log($"[DISCONNECTING] NetworkJobsManager Deinitializing");
            Object.DestroyImmediate(networkManager.GetComponent<NetworkJobsManager>());
            Main.Log($"[DISCONNECTING] NetworkTrainManager Deinitializing");
            Object.DestroyImmediate(networkManager.GetComponent<NetworkTrainManager>());
            Main.Log($"[DISCONNECTING] NetworkJunctionManager Deinitializing");
            Object.DestroyImmediate(networkManager.GetComponent<NetworkJunctionManager>());
            Main.Log($"[DISCONNECTING] NetworkTurntableManager Deinitializing");
            Object.DestroyImmediate(networkManager.GetComponent<NetworkTurntableManager>());
            Main.Log($"[DISCONNECTING] NetworkDebtManager Deinitializing");
            Object.DestroyImmediate(networkManager.GetComponent<NetworkDebtManager>());
            Main.Log($"[DISCONNECTING] NetworkSaveGameManager Deinitializing");
            networkManager.GetComponent<NetworkSaveGameManager>().PlayerDisconnect();
            yield return new WaitUntil(() => networkManager.GetComponent<NetworkSaveGameManager>().IsOfflineSaveLoaded);
            Object.DestroyImmediate(networkManager.GetComponent<NetworkSaveGameManager>());
        }

        /// <summary>
        /// Gets the value if the current local user is connected with a client.
        /// </summary>
        /// <returns>If the user is connected to a server as client</returns>
        public static bool IsClient()
        {
            return isClient;
        }

        /// <summary>
        /// Gets the value if the current local user is hosting a server.
        /// </summary>
        /// <returns>If the user is hosting a server</returns>
        public static bool IsHost()
        {
            return _isHost;
        }
    }
}
