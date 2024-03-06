using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using TMPro;
public class NetworkConnect : MonoBehaviour
{
    public int maxConnection = 20;
    public UnityTransport transport;

    public TextMeshProUGUI debugText;

    private Lobby currentLobby;
    private float heartBeatTimer;
    private async void Awake()
    {
        if (debugText != null) { debugText.text += "Awake Called."; };

        try
        {        
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        } catch (Exception e)
        {
            if (debugText != null) { debugText.text += "Initialization failed. Error: " + e; };
        }
        if (debugText != null) { debugText.text += "Initialized. \n"; };


        JoinOrCreate();
    }

    public async void JoinOrCreate()
    {
        try
        {
            // Quick-join a random lobby with a maximum capacity of 10 or more players.
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions();

            options.Filter = new List<QueryFilter>(){
                new QueryFilter(
                    field: QueryFilter.FieldOptions.MaxPlayers,
                    op: QueryFilter.OpOptions.GE,
                    value: ""+maxConnection)
            };

            Debug.LogError("Trying to join Lobby");
            if(debugText != null) { debugText.text += "Trying to join Lobby"; };
            currentLobby = await Lobbies.Instance.QuickJoinLobbyAsync(options);
            string relayJoinCode = currentLobby.Data["JOIN_CODE"].Value;

            Debug.LogError("Got join code: " + relayJoinCode);
            if (debugText != null) { debugText.text += "Got Join code" + relayJoinCode; };

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            transport.SetClientRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, allocation.HostConnectionData);

            NetworkManager.Singleton.StartClient();
        } catch (Exception ex) 
        {
            Debug.LogError(ex.ToString());
            Debug.LogError("No lobby found, creating lobby");
            if (debugText != null) { debugText.text += "No lobby found, creating lobby"; };
            Create();
        }       
    
    }
    public async void Create()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnection);
        string newJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.LogError("New Join Code: " + newJoinCode);
        if (debugText != null) { debugText.text += "New Join Code: " + newJoinCode; };

        transport.SetHostRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);

        
        CreateLobbyOptions lobbyOptions = new CreateLobbyOptions();
        lobbyOptions.IsPrivate = false;
        lobbyOptions.Data = new Dictionary<string, DataObject>();
        DataObject dataObject = new DataObject(DataObject.VisibilityOptions.Public, newJoinCode);
        lobbyOptions.Data.Add("JOIN_CODE", dataObject);

        currentLobby = await Lobbies.Instance.CreateLobbyAsync("Lobby Name", maxConnection, lobbyOptions);
        

        NetworkManager.Singleton.StartHost();
    }

    public async void Join()
    {
        currentLobby = await Lobbies.Instance.QuickJoinLobbyAsync();
        string relayJoinCode = currentLobby.Data["JOIN_CODE"].Value;

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

        transport.SetClientRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, allocation.HostConnectionData);

        NetworkManager.Singleton.StartClient();
    }


    public async void JoinWithCode(string code)
    {
        //currentLobby = await Lobbies.Instance.QuickJoinLobbyAsync();        

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);

        transport.SetClientRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, allocation.HostConnectionData);

        NetworkManager.Singleton.StartClient();
    }

    private void Update()
    {
        if(heartBeatTimer > 15)
        {
            heartBeatTimer -= 15;

            if(currentLobby != null && currentLobby.HostId == AuthenticationService.Instance.PlayerId)
            {
                LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }

            heartBeatTimer += Time.deltaTime;
        }
    }
}
