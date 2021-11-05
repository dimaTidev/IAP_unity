using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Purchasing;


[CreateAssetMenu(fileName = "IAP_data", menuName = "ScriptableObjects/IAP/Data")]
public class Iap_data : ScriptableObject
{
    [SerializeField] string productID = ""; //Product ID
    [SerializeField] ProductType productType = ProductType.Consumable;
    [SerializeField] string m_name = "Test purchase";
    [SerializeField] Sprite sprite;

    [SerializeField] string prize = "1k money";
    [Tooltip("only for preview")] [SerializeField] string previewPrice = "1 $";

    [SerializeField] UnityEvent onTake = null;


    public void AddProduct(ref ConfigurationBuilder builder) => builder.AddProduct(productID, productType);
    public void BuyProduct() => IAP_Manager.Instance.BuyProductID(this);
    public void TakeProduct()
    {
        Debug.Log("TakeProduct: " + productID);
        onTake?.Invoke();
    }

    public string ProductID => productID;
    public string Name => m_name;
    public Sprite Sprite => sprite;

    public string Prize => prize;
    public string PreviewPrice => previewPrice;
}
