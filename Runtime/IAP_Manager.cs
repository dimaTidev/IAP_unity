#define LOG_USE
//#define RECEIPT_VALIDATION
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections;
using UnityEngine.Store; // UnityChannel

#if RECEIPT_VALIDATION
using UnityEngine.Purchasing.Security;
#endif

/// <summary>
/// All IAP_data scriptableObjects must be place in path - "Resources/IAP/"
/// </summary>
public class IAP_Manager : Singleton<IAP_Manager>, IStoreListener
{
    [Header("IAP_data must be in-\"Resources/IAP/Data\"")]
    [SerializeField] string pathToIAP_data = "IAP/Data/";
    [SerializeField] bool isDontDestroyOnLoad = false;
    [SerializeField] bool isInitializeInStart = true;
#pragma warning disable 0414
    private bool m_IsGooglePlayStoreSelected;
#pragma warning restore 0414
    private bool m_IsUnityChannelSelected;
    private bool m_FetchReceiptPayloadOnPurchase = false;


#if RECEIPT_VALIDATION
    private CrossPlatformValidator validator;
#endif

    private static IStoreController m_StoreController;
    private static IExtensionProvider m_StoreExtensionProvider;


    Action callback_OnInitializeCompleted;
    public void Subscribe_OnInitializeCompleted(Action callback)
    {
        if (IsInitialized)
            callback.Invoke();
        callback_OnInitializeCompleted += callback;
    }

    Action<bool> callback_OnPurchased;
    public void Subscribe_OnPurchased(Action<bool> callback) => callback_OnPurchased += callback;
    public void UnSubscribe_OnPurchased(Action<bool> callback) => callback_OnPurchased -= callback;

    Action<int> callback_onNoAdsPurchased;
    public void Subscribe_OnNoAdsPurchased(Action<int> callback) => callback_onNoAdsPurchased += callback;
    public void Unsubscribe_OnNoAdsPurchased(Action<int> callback) => callback_onNoAdsPurchased -= callback;

    //public string GetProductNoADSTest()
    //{        
    //    Product product = m_StoreController.products.WithID(pNoAds);
    //    string str = "";
    //    str += "hasReceipt: " + product.hasReceipt.ToString() + "\n";
    //    str += "receipt: " + product.receipt + "\n";
    //    str += "transactionID: " + product.transactionID + "\n";
    //    str += "availableToPurchase: " + product.availableToPurchase.ToString() + "\n";

    //    return str;
    //}

    //public bool GetPaymentProduct(string productId)//проверка куплен ли уже продукт
    //{
    //    if (!IsInitialized())
    //    {
    //        InitializePurchasing();
    //        InitLocal = true;
    //    }
    //    Product product = m_StoreController.products.WithID(productId);
    //    return product.hasReceipt;
    //}

    protected override void Awake()
    {
        base.Awake();

        if (isDontDestroyOnLoad)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

#if RECEIPT_VALIDATION
        string appIdentifier;
#if UNITY_5_6_OR_NEWER
        appIdentifier = Application.identifier;
#else
        appIdentifier = Application.bundleIdentifier;
#endif
        validator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), UnityChannelTangle.Data(), appIdentifier);
#endif

#if UNITY_EDITOR
        var module = StandardPurchasingModule.Instance();
        // The FakeStore supports: no-ui (always succeeding), basic ui (purchase pass/fail), and
        // developer ui (initialization, purchase, failure code setting). These correspond to
        // the FakeStoreUIMode Enum values passed into StandardPurchasingModule.useFakeStoreUIMode.
        module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;

        var builder = ConfigurationBuilder.Instance(module);
        // Set this to true to enable the Microsoft IAP simulator for local testing.
        builder.Configure<IMicrosoftConfiguration>().useMockBillingSystem = false;
#endif

        m_IsGooglePlayStoreSelected = Application.platform == RuntimePlatform.Android;// && module.appStore == AppStore.GooglePlay;
        //Debug.Log($"m_IsGooglePlayStoreSelected: {m_IsGooglePlayStoreSelected}");
    }
    void Start()
    {
       // m_StoreController = null;
       // m_StoreExtensionProvider = null;
        if (isInitializeInStart)
            InitializePurchasing();
    }
    
    #region Initialization
    //-----------------------------------------------------------------------------------------------------
    // InitializePurchasing => ( или OnInitialized или OnInitializeFailed ) => InitCallbackNullable
    // инициализация        =>       все нормально     что-то пошло не так  => Очистить калбэки и вызвать нужный

    Action onInitSuccess;
    Action onInitFail;

    public bool IsInitialized => m_StoreController != null && m_StoreExtensionProvider != null;

    Iap_data[] IAP_data;

    /// <summary>
    /// Инициализация ID покупок
    /// </summary>
    public void InitializePurchasing(Action callback_OnSuccess = null, Action callback_OnFail = null)
    {
        if (IsInitialized)
            return;

        onInitSuccess += callback_OnSuccess;
        onInitFail += callback_OnFail;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        IAP_data = Resources.LoadAll<Iap_data>(pathToIAP_data);

        for (int i = 0; i < IAP_data.Length; i++)
            IAP_data[i].AddProduct(ref builder);

        UnityPurchasing.Initialize(this, builder);

        //TODO: Initialize loading
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
#if LOG_USE
        Debug.Log("OnInitialized: Completed! Products: " + controller.products.all.Length);
#endif
        m_StoreController = controller;
        m_StoreExtensionProvider = extensions;
        /*for (int i = 0; i < m_StoreController.products.all.Length; i++)
        {
            Debug.Log($"locPriceStr {m_StoreController.products.all[i].metadata.localizedPriceString} " +
                $"_ locPrice {m_StoreController.products.all[i].metadata.localizedPrice}" +
                $" : {m_StoreController.products.all[i].metadata.isoCurrencyCode}" +
                $": {m_StoreController.products.all[i].metadata.localizedTitle} " +
                $": {m_StoreController.products.all[i].metadata.localizedDescription}");

        }*/
        callback_OnInitializeCompleted?.Invoke();

        InitCallbackNullable(onInitSuccess);
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
#if LOG_USE
        Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
#endif
        InitCallbackNullable(onInitFail);
    }

    void InitCallbackNullable(Action toInvoke)
    {
        toInvoke?.Invoke();
        onInitSuccess = null;
        onInitFail = null;
    }
    //-----------------------------------------------------------------------------------------------------
    #endregion

    public void BuyProductID(Iap_data iap_data)
    {
        if (!iap_data) return;
        BuyProductID(iap_data.ProductID);
    }

    public void BuyProductID(string productId)
    {
        try
        {
            if (IsInitialized)
            {
                Product product = m_StoreController.products.WithID(productId);

                if (product != null && product.availableToPurchase)
                {
#if LOG_USE
                    Debug.Log(string.Format("Purchasing product asychronously: '{0}'", product.definition.id));// ... buy the product. Expect a response either through ProcessPurchase or OnPurchaseFailed asynchronously.
#endif
                    m_StoreController.InitiatePurchase(product);
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
#endif
                }
            }
            else
            {
#if LOG_USE
                Debug.Log("BuyProductID FAIL. Not initialized.");
#endif
            }
        }
#pragma warning disable CS0168 // Переменная объявлена, но не используется
        catch (Exception e)
#pragma warning restore CS0168 // Переменная объявлена, но не используется
        {
#if LOG_USE
            Debug.Log("BuyProductID: FAIL. Exception during purchase. " + e);
#endif
        }

    }

    public void RestorePurchases()
    {
        if (!IsInitialized)
        {
#if LOG_USE
            Debug.Log("RestorePurchases FAIL. Not initialized.");
#endif
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
        {
#if LOG_USE
            Debug.Log("RestorePurchases started ...");
#endif

            var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((result) =>
            {
#if LOG_USE
                Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
#endif
            });
        }
        else
        {
#if LOG_USE
            Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
#endif
        }
    }

    // Успешная покупка
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        //        Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", args.purchasedProduct.definition.id));
        /*if (String.Equals(args.purchasedProduct.definition.id, SKU_money_20k, StringComparison.Ordinal))
        {
            //Action for money
            PPrefs.SetInt("Buy_no_ads", 1);
            gameObject.SendMessage("NOADSButtonHide");
            print("Успешно прошла покупка no_ads");
        }*/

        //    string keyNoAds = "noAds";

        //   string[] skus = GetSKUs;

        //    int[] values = Get_Values;

        Iap_data data = null;

        string curSKU = args.purchasedProduct.definition.id;

        for (int i = 0; i < IAP_data.Length; i++)
        {
            if (string.Equals(curSKU, IAP_data[i].ProductID, StringComparison.Ordinal))
            {
                data = IAP_data[i];
                break;
            }
        }

        if (data == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("IAP: no product");
#endif
            return PurchaseProcessingResult.Complete;
        }

        bool isCompleted = false;

        // Debug.Log($"args.purchasedProduct.transactionID: {args.purchasedProduct.transactionID}");

#if RECEIPT_VALIDATION && !UNITY_EDITOR // Local validation is available for GooglePlay, Apple, and UnityChannel stores

#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
        if (m_IsGooglePlayStoreSelected ||
            (m_IsUnityChannelSelected && m_FetchReceiptPayloadOnPurchase) ||
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.tvOS) 
        {
            try {
                var result = validator.Validate(args.purchasedProduct.receipt);
#if DEVELOPMENT_BUILD
                Debug.Log("Receipt is valid. Contents:");
#endif  //DEVELOPMENT_BUILD
                foreach (IPurchaseReceipt productReceipt in result) 
                {
//                    Debug.Log(productReceipt.productID);
//                    Debug.Log(productReceipt.purchaseDate);
//                    Debug.Log(productReceipt.transactionID);

                    //Mathfz.Calculate(productReceipt.productID, productReceipt.purchaseDate.ToString(), productReceipt.transactionID);

                    /*GooglePlayReceipt google = productReceipt as GooglePlayReceipt;
                    if (null != google) 
                    {
                        Debug.Log(google.purchaseState);
                        Debug.Log(google.purchaseToken);
                    }

                    UnityChannelReceipt unityChannel = productReceipt as UnityChannelReceipt;
                    if (null != unityChannel) {
                        Debug.Log(unityChannel.productID);
                        Debug.Log(unityChannel.purchaseDate);
                        Debug.Log(unityChannel.transactionID);
                    }

                    AppleInAppPurchaseReceipt apple = productReceipt as AppleInAppPurchaseReceipt;
                    if (null != apple) {
                        Debug.Log(apple.originalTransactionIdentifier);
                        Debug.Log(apple.subscriptionExpirationDate);
                        Debug.Log(apple.cancellationDate);
                        Debug.Log(apple.quantity);
                    }*/

//                    Debug.Log($"IAP: productReceipt.productID: {productReceipt.productID} == {data.ProductID}");
                    if (productReceipt.productID == data.ProductID)
                    {
                        isCompleted = true;
                        data.TakeProduct();

                       // if (values[id] == 0)
                       // {
                       //     PPrefs.SetInt_safe(keyNoAds, 1);
                       //     callback_onNoAdsPurchased?.Invoke(1);
                       // }else
                       //     First_Counter.Calculate(values[id], Mathfz.Types.Iap);
                        //Debug.Log("IAP: successfull");
                    }

                    // For improved security, consider comparing the signed
                    // IPurchaseReceipt.productId, IPurchaseReceipt.transactionID, and other data
                    // embedded in the signed receipt objects to the data which the game is using
                    // to make this purchase.
                }
            } 
            catch (IAPSecurityException ex) //Если была попытка взлома покупок
            {
#if DEVELOPMENT_BUILD  //---------------
                Debug.Log("Invalid receipt, not unlocking content. " + ex);
#endif //DEVELOPMENT_BUILD----------------

               // Mathfz.Calculate(args.purchasedProduct.definition.id, "ex", args.purchasedProduct.transactionID);
               //
               // int value = 0;
               // if (id >= 0 && id < values.Length)
               //     value = values[id];
               //
               // PPrefs.Add_Money(value); //Добавить фейковые деньги
               // Mathfz.Calulate(value, "");//Сохранить попытку взлома покупок
                return PurchaseProcessingResult.Complete;
            }
        }
        else
        {
#if DEVELOPMENT_BUILD //---------------
            Debug.LogWarning($"Something is wrong with IAP confirm. m_IsGooglePlayStoreSelected: {m_IsGooglePlayStoreSelected}");
#endif //DEVELOPMENT_BUILD----------------
        }
#endif //UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX

#else //Если нету потверждения RECEIPT_VALIDATION или если в UnityEditor

        data.TakeProduct();
        //   if (values[id] == 0)
        //   {
        //       PPrefs.SetInt_safe(keyNoAds, 1);
        //       callback_onNoAdsPurchased?.Invoke(1);
        //   } else
        //       First_Counter.Calculate(values[id], Mathfz.Types.Iap);
        //
           isCompleted = true;
        //   Mathfz.Calculate(args.purchasedProduct.definition.id, "", args.purchasedProduct.transactionID);
#if LOG_USE
        Debug.Log("IAP: successfull without validation");
#endif //LOG_USE ---------
#endif

        callback_OnPurchased?.Invoke(isCompleted);
        callback_OnPurchased = null;

        //PPrefs.SetInt_safeSalted(pprefs_key_plMon, pl_m);
        return PurchaseProcessingResult.Complete;
    }
    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        callback_OnPurchased?.Invoke(false);
        callback_OnPurchased = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
#endif
    }

}

