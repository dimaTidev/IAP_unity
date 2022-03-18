using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class IAP_Button : MonoBehaviour, IPointerClickHandler
{
   // [DrawScriptableObjectInInspector]
    [SerializeField] Iap_data data = null;

    [SerializeField] Image image_icon = null;
    [SerializeField] Text 
        //text_name = null,
        text_price = null,
        text_prize = null;

    void OnEnable()
    {
        IAP_InitializeBroker.TryOnCheck(() => Refresh(data));
        Refresh(data);
    }

    void Refresh(Iap_data data)
    {
        if (!data)
            return;

        if (text_price)
            text_price.text = data.PreviewPrice;

        if (text_prize)
            text_prize.text = data.Prize;

        if (image_icon)
            image_icon.sprite = data.Sprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
       // print("Try Buy");
        if (data)
            IAP_InitializeBroker.TryOnCheck(data.BuyProduct);
    }
}
