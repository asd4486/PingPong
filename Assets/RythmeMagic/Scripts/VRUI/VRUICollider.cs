﻿using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class VRUICollider : MonoBehaviour
{
    private BoxCollider boxCollider;
    private RectTransform rectTransform;

    private void OnEnable()
    {
        ValidateCollider();
    }

    private void OnValidate()
    {
        ValidateCollider();
    }

    private void ValidateCollider()
    {
        rectTransform = GetComponent<RectTransform>();

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        boxCollider.size = new Vector3(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y, 10f);
    }
}