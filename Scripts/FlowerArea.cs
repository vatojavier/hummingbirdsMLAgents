using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages collections of flower and plants
/// </summary>
public class FlowerArea : MonoBehaviour
{
    //diameter of the flower and agent can be
    public const float AreaDiameter = 20f;

    //List of flowerplants in the area
    private List<GameObject> flowerPlants;

    //Lookup dictionary for looking flower from a nectar collider
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    public List<Flower> Flowers { get; private set; }

    /// <summary>
    /// Reset flowers and plants
    /// </summary>
    public void ResetFlowers()
    {
        //Rotate a bit
        foreach (GameObject flowePlant in flowerPlants)
        {
            float xRotation = UnityEngine.Random.Range(-5f, 5f);
            float yRotation = UnityEngine.Random.Range(-180f, 180f);
            float zRotation = UnityEngine.Random.Range(-5f, 5f);
            flowePlant.transform.rotation = Quaternion.Euler(xRotation, yRotation, zRotation);
        }

        foreach(Flower flower in Flowers)
        {
            flower.ResetFlower();
        }
    }

    /// <summary>
    /// Gets flower the nectar belongs to
    /// </summary>
    /// <param name="collider">The nectar collider</param>
    /// <returns></returns>
    public Flower GetFlowerFromNectar(Collider collider)
    {
        return nectarFlowerDictionary[collider];
    }


    private void Awake()
    {
        //Initiaize variables
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();

        //Find all flowers that are children of this gameobject/transform
        FindChildFlowers(transform);
    }

    /// <summary>
    /// When game starts...
    /// </summary>
    private void Start()
    {
        
        
    }

    /// <summary>
    /// Recursively find all flwers and plantFlowers
    /// </summary>
    /// <param name="parent"></param>
    private void FindChildFlowers(Transform parent)
    {
        for(int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.CompareTag("flower_plant"))
            {
                //Found flowerplant, add it to flowerplants list
                flowerPlants.Add(child.gameObject);

                //Look for flowers in the flowerplant
                FindChildFlowers(child);
            }
            else
            {
                //Not a flower plant, look for a flower component
                Flower flower = child.GetComponent<Flower>();
                if(flower != null)
                {
                    Flowers.Add(flower);

                    //Add nectar collider to the lookup dictoniary
                    nectarFlowerDictionary.Add(flower.nectarCollider, flower);

                    //Note no flowers hat are chldren of other fowers

                }
                else
                {
                    //Flower not found -> check children
                    FindChildFlowers(child);
                }

            }
        }
    }

}
