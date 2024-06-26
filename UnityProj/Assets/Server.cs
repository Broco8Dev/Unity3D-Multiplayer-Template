using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using NaughtyAttributes;
using System.Threading.Tasks;
using TMPro;

public class Server : MonoBehaviour
{
    public TMP_InputField ipInput;
    private string serverUrl = "http://localhost:8000";
    public GameObject lobbyUI;
    private const float updateInterval = 0.0001f;

    public Dictionary<string, string> ServerDictionary { get; private set; } = new Dictionary<string, string>();

    private Dictionary<string, GameObject> dummyPlayers = new Dictionary<string, GameObject>();
    public int DictionaryKeyCount => ServerDictionary.Count;

    [SerializeField] private Transform playerPrefab;
    [SerializeField] private GameObject dummyPlayerPrefab;
    private Transform player;
    private string playerTag;
    public bool connected = false;

    private Vector3 lastPlayerPosition;
    private readonly WaitForSeconds updateWait = new WaitForSeconds(updateInterval);

    public bool singlePlayer = false;

    private void Start()
    {
        if (singlePlayer)
        {
            lobbyUI.SetActive(false);
            connected = true;
            _ = InitializeOffline();
        }

    }

    public void initialize()
    {

        serverUrl = ipInput.text;
        lobbyUI.SetActive(false);
        _ = InitializeServerAsync();
    }

    private async Task InitializeOffline()
    {
        await CreateLocalPlayerAsync();
    }

    private async Task InitializeServerAsync()
    {
        if (!Application.isPlaying) return;
        await GetDictionaryAsync();
        await CreateLocalPlayerAsync();
        connected = true;
        CreateOtherPlayers();
        _ = UpdateLoopAsync();
    }

    private async Task CreateLocalPlayerAsync()
    {
        playerTag = $"P{DictionaryKeyCount + 1}";
        player = Instantiate(playerPrefab);
        await Task.Yield();
    }

    private void CreateOtherPlayers()
    {
        foreach (var kvp in ServerDictionary)
        {
            if (kvp.Key != playerTag)
            {
                CreateOrUpdateDummyPlayer(kvp.Key, kvp.Value);
            }
        }
    }

    private async Task UpdateLoopAsync()
    {
        while (true)
        {
            if (player != null)
            {
                Vector3 currentPosition = player.position;
                if (currentPosition != lastPlayerPosition)
                {
                    _ = UploadAsync(playerTag, $"{currentPosition.x} {currentPosition.y} {currentPosition.z}");
                    lastPlayerPosition = currentPosition;
                }
            }

            await GetDictionaryAsync();

            foreach (var kvp in ServerDictionary)
            {
                if (kvp.Key != playerTag)
                {
                    CreateOrUpdateDummyPlayer(kvp.Key, kvp.Value);
                }
            }

            await Task.Delay(Mathf.RoundToInt(updateInterval * 1000));
        }
    }

    private void CreateOrUpdateDummyPlayer(string key, string value)
    {
        Vector3 newPosition = ParsePosition(value);

        if (dummyPlayers.TryGetValue(key, out GameObject dummyPlayer))
        {
            dummyPlayer.transform.position = newPosition;
        }
        else
        {
            dummyPlayers[key] = Instantiate(dummyPlayerPrefab, newPosition, Quaternion.identity);
        }
    }

    private Vector3 ParsePosition(string positionString)
    {
        string[] stringCoords = positionString.Split(' ');
        return new Vector3(
            float.Parse(stringCoords[0]),
            float.Parse(stringCoords[1]),
            float.Parse(stringCoords[2])
        );
    }

    private async Task UploadAsync(string key, string value)
    {
        string json = JsonUtility.ToJson(new UploadData { key = key, value = value });
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/upload", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            var operation = www.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error uploading: " + www.error);
            }
        }
    }

    private async Task GetDictionaryAsync()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(serverUrl + "/get"))
        {
            var operation = www.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (www.result == UnityWebRequest.Result.Success)
            {
                ServerDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError("Error getting dictionary: " + www.error);
            }
        }
    }

    [Button("Inject Player")]
    private void InjectPlayer()
    {
        _ = InjectPlayerAsync();
    }

    private async Task InjectPlayerAsync()
    {
        await GetDictionaryAsync();
        string newPlayerKey = $"P{DictionaryKeyCount + 1}";
        await UploadAsync(newPlayerKey, "0 0 0");
        Debug.Log($"Injected Player {newPlayerKey}");

        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(Mathf.RoundToInt(updateInterval * 1000));
            _ = UploadAsync(newPlayerKey, $"{i} 0 0");
        }
    }

    [System.Serializable]
    private class UploadData
    {
        public string key;
        public string value;
    }
}