#define LOG_USE
#define RECEIPT_VALIDATION
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections;

#if RECEIPT_VALIDATION
using UnityEngine.Purchasing.Security;
#endif

//TODO: Remove Singleton or Write autoDestroy
//TODO: Add Apple!

/// <summary>
/// All IAP_data scriptableObjects must be place in path - "Resources/IAP/"
/// </summary>
public class IAP_Manager : Singleton<IAP_Manager>, IStoreListener
{
    [Header("IAP_data must be in-\"Resources/IAP/Data\"")]
    [SerializeField] string pathToIAP_data = "IAP/Data/";

#if RECEIPT_VALIDATION
    private CrossPlatformValidator m_Validator;
#endif

    private static IStoreController m_StoreController;
    private static IExtensionProvider m_StoreExtensionProvider;

    Iap_data Iap_Data_bySKU(string curSKU)
    {
        if (IAP_data == null)
            return null;
        for (int i = 0; i < IAP_data.Length; i++)
            if (string.Equals(curSKU, IAP_data[i].ProductID, StringComparison.Ordinal))
                return IAP_data[i];
        return null;
    }

    public Product Get_ProductByID(string SKU) => m_StoreController.products.WithStoreSpecificID(SKU);

    #region InfoCallbacks
    //-----------------------------------------------------------------------------------------------------------------------------------------------------------
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
    //-----------------------------------------------------------------------------------------------------------------------------------------------------------
    #endregion
    
  
    protected override void Awake()
    {
        base.Awake();

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

#if RECEIPT_VALIDATION
        InitializeValidator();
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
    }

    #region Initialization
    //-------------------------------------------------------------------------------------------------------------------------------------------------
    // InitializePurchasing => ( или OnInitialized или OnInitializeFailed ) => InitCallbackNullable
    // инициализация        =>       все нормально     что-то пошло не так  => Очистить калбэки и вызвать нужный

    Action onInitSuccess;
    Action onInitFail;

    public bool IsInitialized => m_StoreController != null && m_StoreExtensionProvider != null;

    Iap_data[] IAP_data;

    /// <summary>
    /// Инициализация ID покупок
    /// </summary>
    public void InitializePurchasing(Action callback_OnSuccess = null, Action onInitFail = null)
    {
        //Checks ----------------------
        if (IsInitialized)
            return;
        onInitSuccess += callback_OnSuccess;
        this.onInitFail += onInitFail;

        //Prepare ----------------------
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        IAP_data = Resources.LoadAll<Iap_data>(pathToIAP_data);
        if(IAP_data == null)
        {
            Debug.LogError("Can't load IAP products");
            return;
        }

        for (int i = 0; i < IAP_data.Length; i++)
            IAP_data[i].AddProduct(ref builder);

        //Initialize --------------------
        UnityPurchasing.Initialize(this, builder);
    }

    void InitCallbackNullable(Action toInvoke)
    {
        toInvoke?.Invoke();
        onInitSuccess = null;
        onInitFail = null;
    }
    //-------------------------------------------------------------------------------------------------------------------------------------------------
    #endregion

    #region BuyInvoke
    //-------------------------------------------------------------------------------------------------------------------------------------------------
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
                    SystemMessage.Log("Product is not available for purchase", SystemMessage.MsgType.Error); // Not purchasing product, either is not found or is not available for purchase
            }
            else
            {
#if LOG_USE
                Debug.Log("BuyProductID FAIL. Not initialized.");
#endif
            }
        }
        catch (Exception e)
        {
            SystemMessage.Log("Buy Product FAIL: " + e, SystemMessage.MsgType.Error);
        }
    }
    //-------------------------------------------------------------------------------------------------------------------------------------------------
    #endregion

    #region IStoreListener
    //-------------------------------------------------------------------------------------------------------------------------------------------------
    void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        m_StoreController = controller;
        m_StoreExtensionProvider = extensions;

        callback_OnInitializeCompleted?.Invoke();
        InitCallbackNullable(onInitSuccess);
    }
    void IStoreListener.OnInitializeFailed(InitializationFailureReason error) => InitCallbackNullable(onInitFail);
    // Успешная покупка
    PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs args)
    {
        var product = args.purchasedProduct;
        string curSKU = product.definition.id;

        Iap_data data = Iap_Data_bySKU(curSKU);

        if (data == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("IAP: no product");
#endif
            return PurchaseProcessingResult.Complete;
        }
        // Debug.Log($"args.purchasedProduct.transactionID: {args.purchasedProduct.transactionID}");
        bool isCompleted = false; //need only for callback

#if RECEIPT_VALIDATION && !UNITY_EDITOR
        if (IsPurchaseValid(product))
        {
            isCompleted = true;
            data.TakeProduct();
        }
#else
        data.TakeProduct();
        isCompleted = true;
        #if LOG_USE
        Debug.Log("IAP: successfull without validation");
        #endif //LOG_USE ---------
#endif

        callback_OnPurchased?.Invoke(isCompleted);
        callback_OnPurchased = null;
        return PurchaseProcessingResult.Complete;
    }
    void IStoreListener.OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        SystemMessage.Log($"Purchase failed: {failureReason.ToString()}", SystemMessage.MsgType.Error);
        callback_OnPurchased?.Invoke(false);
        callback_OnPurchased = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
#endif
    }
    //-------------------------------------------------------------------------------------------------------------------------------------------------
    #endregion IStoreListener

    #region Commented
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


    /*public void RestorePurchases()
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
    }*/

    #endregion

    #region Validator
    //------------------------------------------------------------------------------------------------------------------------------------------------
#if RECEIPT_VALIDATION
    bool m_UseAppleStoreKitTestCertificate;
    void InitializeValidator()
    {
        if (IsCurrentStoreSupportedByValidator())
        {
#if !UNITY_EDITOR
                var appleTangleData = m_UseAppleStoreKitTestCertificate ? AppleStoreKitTestTangle.Data() : AppleTangle.Data();
                m_Validator = new CrossPlatformValidator(GooglePlayTangle.Data(), appleTangleData, Application.identifier);
#endif
        }
        else
            Debug.LogWarning("Ivalid store: " + StandardPurchasingModule.Instance().appStore);
    }
    bool IsCurrentStoreSupportedByValidator() => IsGooglePlayStoreSelected() || IsAppleAppStoreSelected();  //The CrossPlatform validator only supports the GooglePlayStore and Apple's App Stores.
    bool IsGooglePlayStoreSelected()
    {
        var currentAppStore = StandardPurchasingModule.Instance().appStore;
        return currentAppStore == AppStore.GooglePlay;
    }
    bool IsAppleAppStoreSelected()
    {
        var currentAppStore = StandardPurchasingModule.Instance().appStore;
        return currentAppStore == AppStore.AppleAppStore ||
               currentAppStore == AppStore.MacAppStore;
    }
    bool IsPurchaseValid(Product product)
    {
        if (IsCurrentStoreSupportedByValidator()) //If we the validator doesn't support the current store, we assume the purchase is valid
        {
            try
            {
                var result = m_Validator.Validate(product.receipt);
                //LogReceipts(result); //The validator returns parsed receipts.
            }
            catch (IAPSecurityException reason) //If the purchase is deemed invalid, the validator throws an IAPSecurityException.
            {
                Debug.Log($"Invalid receipt: {reason}");
                return false;
            }
        }
        return true;
    }
    void LogReceipt(IPurchaseReceipt receipt)
    {
        Debug.Log($"Product ID: {receipt.productID}\n" +
                  $"Purchase Date: {receipt.purchaseDate}\n" +
                  $"Transaction ID: {receipt.transactionID}");

        if (receipt is GooglePlayReceipt googleReceipt)
        {
            Debug.Log($"Purchase State: {googleReceipt.purchaseState}\n" +
                      $"Purchase Token: {googleReceipt.purchaseToken}");
        }

        if (receipt is AppleInAppPurchaseReceipt appleReceipt)
        {
            Debug.Log($"Original Transaction ID: {appleReceipt.originalTransactionIdentifier}\n" +
                      $"Subscription Expiration Date: {appleReceipt.subscriptionExpirationDate}\n" +
                      $"Cancellation Date: {appleReceipt.cancellationDate}\n" +
                      $"Quantity: {appleReceipt.quantity}");
        }
    }
#endif
    //------------------------------------------------------------------------------------------------------------------------------------------------
    #endregion

}

