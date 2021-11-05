using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

//TODO: проверить интернет перед покупкой и инициализацию IAP_manager - это должно вызвать fullScreenLoad
//TODO: проверить интернет в OnEnable кнопок и инициализировать IAP_manager - это должно вызвать fullScreenLoad
//      соответственно проверка интернета и инициализация должны перекрывать весь экран

//TODO: Если IAP_manager не смог инициализироваться то выдать сообщение на экран

public static class IAP_InitializeBroker
{
    [Header("IAP_Manager must be in-\"Resources/IAP/IAP_Manager\"")]
    [SerializeField] const string pathToIAP_Manager = "IAP/IAP_Manager";

    static IAP_Manager prefabManager => Resources.Load<IAP_Manager>(pathToIAP_Manager);

    static Action callbackOnCompleted;

    public static void TryOnCheck(Action _callbackOnCompleted = null)
    {
        if(_callbackOnCompleted != null)
            callbackOnCompleted += _callbackOnCompleted;

        //TODO: Internet Check
        DateTimeInternet.CheckNetworkConnection(OnNetworkCheckCompleted); 
        //OnNetworkCheckCompleted(true); //Test
    }

    /// <summary>
    /// Callback Internet check
    /// </summary>
    static void OnNetworkCheckCompleted(bool isOnline)
    {
        if (isOnline)
        {
            if (!IAP_Manager.Instance)
            {
                IAP_Manager prefab = prefabManager;
                if (!prefab)
                    return;
                GameObject go = MonoBehaviour.Instantiate(prefab.gameObject);
            }

            if (!IAP_Manager.Instance.IsInitialized)
                IAP_Manager.Instance.InitializePurchasing(OnInitPurchasingSuccess);
            else
                OnComplete();
        }
    }

    static void OnInitPurchasingSuccess() => OnComplete();

    static void OnComplete()
    {
        callbackOnCompleted?.Invoke();
        callbackOnCompleted = null;
    }

    //  public void TryNetworkCheck()
    //  {
    //     if (coroutine != null) return;
    //     
    //     ShowHide_NoInternet(false);
    //     ShowHide_Loading(true);
    //     coroutine = StartCoroutine(NetworkCheck.CheckInternetConnect(OnNetwork));
    //  }

    //void OnNetwork(bool isOnline)
}
