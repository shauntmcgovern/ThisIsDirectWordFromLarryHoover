using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using ExitGames.Client.Photon.LoadBalancing;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PlayFab.ClientModels;

public class PlayFabService : MonoBehaviour {
    public delegate void OnLogExternal(string message);
    public static event OnLogExternal OnLogExternalEvents;

    public WebRequestType RequestType = WebRequestType.UnityWWW;
    public string TitleId = string.Empty;
#if PLAYFABLOCAL
#endif
    public bool IsLocalTesting = true;

    public bool NetworkingEnabled = true;
    public string PlayFabPhotonAppId = string.Empty;

    public static bool IsLoggedIn = false;
    private string _UniqueID = null;
    private PlayFabPhotonAdapter _photon = null;
    private string _PlayFabId = string.Empty;
    private string _PlayFabSessionTicket = string.Empty;

    private bool _timerStart = false;
    private float _timerVal = 0f;

    

    void Awake()
    {
        PlayFabSettings.RequestType = RequestType;
        PlayFabSettings.TitleId = TitleId;
        if (IsLocalTesting) { PlayFabSettings.IsTesting = true; }
        Debug.LogFormat("PlayFab Server Connected To: {0}", PlayFabSettings.GetURL());
        #region Startup and Device Login / Registration
        // Use the line below to subscribe to events.
        // this.OnEvent<MyEvent>().Subscribe(myEventInstance=>{  TODO });

        _UniqueID = SystemInfo.deviceUniqueIdentifier;
        Log(_UniqueID);

#if UNITY_ANDROID
            AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver");
            AndroidJavaClass secure = new AndroidJavaClass("android.provider.Settings$Secure");
            _UniqueID = secure.CallStatic<string>("getString", contentResolver, "android_id");
            PlayFabClientAPI.LoginWithAndroidDeviceID(new PlayFab.ClientModels.LoginWithAndroidDeviceIDRequest()
            {
                AndroidDeviceId = _UniqueID,
                AndroidDevice = SystemInfo.deviceModel,
                OS = SystemInfo.operatingSystem,
                TitleId = PlayFabSettings.TitleId,
                CreateAccount=true
            }, PlayFabLoggedIn, PlayFabLoggedInError);

#elif UNITY_IOS
            //TODO: Get DeviceId for IOS devices.
            PlayFabClientAPI.LoginWithIOSDeviceID(new PlayFab.ClientModels.LoginWithIOSDeviceIDRequest() { 
                DeviceId = _UniqueID,
                DeviceModel = SystemInfo.deviceModel,
                OS = SystemInfo.operatingSystem,
                TitleId = PlayFabSettings.TitleId,
                CreateAccount=true
            },  PlayFabLoggedIn, PlayFabLoggedInError);

#else

        string _password = string.Empty;
        if (PlayerPrefs.HasKey("PlayFabUserPassword"))
        {
            _password = PlayerPrefs.GetString("PlayFabUserPassword");
        }
        else
        {
            _password = _UniqueID.Substring(0, 6);
            PlayerPrefs.SetString("PlayFabUserPassword", _password);
        }

        if (PlayerPrefs.HasKey("IsRegistered"))
        {
            Log("Has Key IsRegistered");
            _timerStart = true;
            PlayFabClientAPI.LoginWithPlayFab(new PlayFab.ClientModels.LoginWithPlayFabRequest()
            {
                TitleId = PlayFabSettings.TitleId,
                Username = _UniqueID.Substring(0, 20),
                Password = _password
            }, PlayFabLoggedIn, PlayFabLoggedInError);
        }
        else
        {
            Log("No IsRegistered Key: " + PlayFabSettings.TitleId);
            string _emailAddress = string.Format("{0}@{1}", _UniqueID.Substring(0, 7), "tempEmailAddress.com");
            Log(string.Format("Sending {0}, {1}, {2}, {3}", PlayFabSettings.TitleId, _emailAddress, _UniqueID, _password));
            PlayFabClientAPI.RegisterPlayFabUser(new PlayFab.ClientModels.RegisterPlayFabUserRequest()
            {
                TitleId = PlayFabSettings.TitleId,
                Email = _emailAddress,
                Username = _UniqueID.Substring(0, 20),
                Password = _password
            }, PlayFabRegistered, PlayFabLoggedInError);
        }

#endif
        #endregion

        if (NetworkingEnabled)
        {
            _photon = new PlayFabPhotonAdapter();
            _photon.AppId = PlayFabPhotonAppId;
            _photon.AppVersion = "1.0";
            _photon.OnStateChangeAction += OnPhotonStatusChanged;
        }
    }

    private void PlayFabRegistered(PlayFab.ClientModels.RegisterPlayFabUserResult result)
    {
        IsLoggedIn = true;
        PlayerPrefs.SetInt("IsRegistered", 1);
    }

    private void PlayFabLoggedIn(PlayFab.ClientModels.LoginResult result)
    {
        _timerStart = false;
        IsLoggedIn = true;
        _PlayFabId = result.PlayFabId;
        _PlayFabSessionTicket = result.SessionTicket;

        if (NetworkingEnabled)
        {
            Log(string.Format("PlayFabId is: {0}", _PlayFabId));

            GetPhotonAuthenticationTokenRequest request = new GetPhotonAuthenticationTokenRequest();
            request.PhotonApplicationId = _photon.AppId.Trim();
            // get an authentication ticket to pass on to Photon 
            Log("Requesting PlayFab Photon Auth Ticket");
            _timerStart = true;
            PlayFabClientAPI.GetPhotonAuthenticationToken(request, OnPhotonAuthenticationSuccess, PlayFabLoggedInError);
        }
    }

    private void PlayFabLoggedInError(PlayFabError error)
    {
        Log(error.ErrorMessage);
        Log(error.Error.ToString());
        if (error.ErrorDetails != null)
        {
            foreach (var kvp in error.ErrorDetails)
            {
                foreach (var e in kvp.Value)
                {
                    Log(e.ToString());
                }
            }
        }
    }


    #region Photon Networking

    private void OnPhotonStatusChanged(ExitGames.Client.Photon.LoadBalancing.ClientState state)
    {
        //Note I separated this into two methods w/ event forwrading just in case we want to handle multiple states.
        Log(string.Format("PlayFab Photon Status Changed: {0}", state));
        if (state == ExitGames.Client.Photon.LoadBalancing.ClientState.ConnectedToMaster)
        {
            ConnectedToPlayFabPhotonHandler();
        }
    }

    private void ConnectedToPlayFabPhotonHandler()
    {
        Log(string.Format("PhotonPlayerID: {0}", _photon.LocalPlayer.ID));
        if (_photon.LocalPlayer.ID == -1)
        {

            var actorProperties = new Hashtable();
            PlayFabClientAPI.GetUserData(new GetUserDataRequest()
            {
                PlayFabId = _PlayFabId,
                Keys = new List<string>() { "Rank" }
            }, (result) =>
            {
                var PlayerRank = "0";
                if (result.Data.ContainsKey("Rank"))
                {
                    PlayerRank = result.Data["Rank"].Value;
                }
                else
                {
                    //No Rank Data was found, which means it has never been set, so setup the data now.
                    var updatedData = new Dictionary<string, string>();
                    updatedData.Add("Rank", PlayerRank);

                    PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                    {
                        Data = updatedData
                    }, (updateUserDataResult) => { }, PlayFabLoggedInError);
                }

                actorProperties.Add("Rank", PlayerRank);
                _photon.CreatePlayer(_PlayFabId, 0, true, actorProperties);
                _photon.OpJoinOrCreateRoom("CrossfireRoom_01", _photon.LocalPlayer.ID, new ExitGames.Client.Photon.LoadBalancing.RoomOptions()
                {
                    CheckUserOnJoin = true,
                    EmptyRoomTtl = 5000,
                    MaxPlayers = 0,
                    PlayerTtl = 1000
                });


            }, PlayFabLoggedInError);

        }
    }

    internal void OnPhotonAuthenticationSuccess(PlayFab.ClientModels.GetPhotonAuthenticationTokenResult result)
    {
        _timerStart = false;
        Log(string.Format("Photon Auth Successful: {0}", result.PhotonCustomAuthenticationToken));
        _photon.ConnectToMasterServer(_PlayFabId, result.PhotonCustomAuthenticationToken);

    }

    void Update()
    {
        if (_timerStart)
        {
            _timerVal += Time.deltaTime;
        }
        if (_timerVal != 0f && _timerStart == false)
        {
            Log(string.Format("Operation Took {0} ms to complete", _timerVal));
            _timerVal = 0f;
        }

        if (_photon != null)
        {
            _photon.Service();
        }
    }

    #endregion

    #region Debugging
    private void Log(string message)
    {
        Debug.Log(message);
        if (OnLogExternalEvents != null)
        {
            OnLogExternalEvents(message);
        }
        
    }
    #endregion

    public void OnApplicationQuit()
    {
        _photon.Disconnect();
    }


}