using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comportantimiento de la flor
/// </summary>
public class Flower : MonoBehaviour
{
    [Tooltip("Color when full")]
    public Color fullFlowerColor = new Color(1f, 0f, .3f);

    [Tooltip("Color when Empty")]
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);

    /// <summary>
    /// The Nectar Trigger collder 
    /// </summary>
    [HideInInspector]
    public Collider nectarCollider;

    private Collider flowerCollider;

    private Material flowerMaterial;

    /// <summary>
    /// The perpendicular vector up out the flower
    /// </summary>
    /// <returns></returns>
    public Vector3 FlowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    public Vector3 FlowerCenterPosition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    public float NectarAmount { get; private set; }

    public bool hasNectar
    {
        get
        {
            return NectarAmount > 0f;
        }
    }

    /// <summary>
    /// Attemps to remove nectar from the flower
    /// </summary>
    /// <param name="amount">Amonyt trried to eat</param>
    /// <returns>Succesfully removed nectar</returns>
    public float Feed(float amount)
    {
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);

        NectarAmount -= amount;
        if(NectarAmount <= 0)
        {
            NectarAmount = 0;

            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);
        }

        return nectarTaken;
    }

    /// <summary>
    /// Resets the flower, dont use Reset() (unity function)
    /// </summary>
    public void ResetFlower()
    {
        NectarAmount = 1f;

        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);

        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);

    }

    private void Awake()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;

        // Find flower and nectar collider
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();

        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }



}
