using UnityEngine;
using System.Collections;
using ExitGames.Client.Photon.LoadBalancing;

public class PlayFabPhotonAdapter : LoadBalancingClient
{
    /*
    protected internal override Player CreatePlayer(string actorName, int actorNumber, bool isLocal, ExitGames.Client.Photon.Hashtable actorProperties)
    {
        return base.CreatePlayer(actorName, actorNumber, isLocal, actorProperties);
    }
    */

    protected internal override Room CreateRoom(string roomName, RoomOptions opt)
    {
        return base.CreateRoom(roomName, opt);
    }

    public override void OnEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        base.OnEvent(photonEvent);
        Debug.Log(string.Format("Recieved Event: {0}", photonEvent.Code));
    }

    public override void OnMessage(object message)
    {
        base.OnMessage(message);
        Debug.Log(string.Format("Recieved Message: {0}", message.ToString()));

    }

    public override void OnStatusChanged(ExitGames.Client.Photon.StatusCode statusCode)
    {
        base.OnStatusChanged(statusCode);
        Debug.Log(string.Format("Recieved Status Code: {0}", statusCode));
    }

    public override void OnOperationResponse(ExitGames.Client.Photon.OperationResponse operationResponse)
    {
        base.OnOperationResponse(operationResponse);
    }

    public override bool OpRaiseEvent(byte eventCode, object customEventContent, bool sendReliable, RaiseEventOptions raiseEventOptions)
    {
        return base.OpRaiseEvent(eventCode, customEventContent, sendReliable, raiseEventOptions);
    }

    public void ConnectToMasterServer(string id, string ticket)
    {
        if (this.CustomAuthenticationValues != null)
        {
            this.CustomAuthenticationValues.SetAuthParameters(id, ticket);
        }
        else
        {
            //Debug.Log(string.Format("Id: {0}, Ticket: {1}", id, ticket));
            this.CustomAuthenticationValues = new AuthenticationValues()
            {
                AuthType = CustomAuthenticationType.Custom,
                Secret = null,
                AuthParameters = null,
            };

            this.CustomAuthenticationValues.SetAuthParameters(id, ticket);
        }
        this.ConnectToRegionMaster("US");  // Turnbased games have to use this connect via Name Server
    }

}