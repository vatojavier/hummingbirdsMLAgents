using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Bird ML agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate up axis")]
    public float yawSpeed = 100f;


    [Tooltip("Tranform at the tip of the beak (pico)")]
    public Transform beakTip;

    [Tooltip("The agent camera (only the player, not the nn)")]
    public Camera agentCamera;

    [Tooltip("Wether is training")]
    public bool trainingMode;

    // rb of the agent
    new private Rigidbody rigidbody;

    //Flower area the agent is in
    private FlowerArea flowerArea;

    private Flower nearestFlower;

    // Smoother pitch changes
    private float smootPithChange = 0f;

    // Smoother yaw changes
    private float smootYawChange = 0f;

    //Maximun angle bird can putch up or down
    private const float MaxPitchAngle = 80f;

    //Radius in the beak to detect wether is in the nectar
    private const float BeakTipRadius = 0.008f;

    //Wether agent is frozen (not flying)
    private bool frozen = false;

    /// <summary>
    /// Amount of nectar the agent has eaten
    /// </summary>
    public float NectarObtained { get; private set; }

    /// <summary>
    /// Initialize the agent
    /// </summary>
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();

        flowerArea = GetComponentInParent<FlowerArea>();

        //If not training, play forever
        if(!trainingMode)
        {
            MaxStep = 0;
        }
    }

    /// <summary>
    /// Resets the agent when a new episode of tranining begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            //Only reset flowers in training when there is one agent per area
            flowerArea.ResetFlowers();
        }

        //Reset nectar obtained
        NectarObtained = 0f;

        //Zero out velocity
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity= Vector3.zero;

        //Default spawning in front of flower
        bool inFrontOfFlower = true;
        if (trainingMode)
        {
            //Spawn in front of flower 50% of times dring training NICE INSTANT REWARD
            inFrontOfFlower = UnityEngine.Random.value > .5f;
        }

        //Move agent to random pos
        MoveToSafeRandomPosition(inFrontOfFlower);

        //Recalculate nearestflower
        UpdateNearestFlower();

    }

    /// <summary>
    /// Called when an action is received from etiher the player input or the neural network
    /// Converting the instruction to movement and rotations.
    /// Are the Space size of the behavior in the Unity Editor!
    /// 
    /// vectorAction[i] represents (decided by us):
    /// Index 0: move x (+1 = right, -1 = left)
    /// Index 1: move y (+1 = up, -1 = down)
    /// Index 2: move z (+1 = forward, -1 = backward)
    /// Index 3: pitch angle (+1 = pitch up, -1 = pitch down)
    /// Index 4: yaw angle (+1 = turn right, -1 = turn left)
    /// </summary>
    /// <param name="vectorAction">The actions to take</param>
    public override void OnActionReceived(float[] vectorAction)
    {
        //Don't take action if frozen (when alredy trained)
        if (frozen) return;

        //Calculate movemnt vector
        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);

        //Add force in direction of movem vector
        rigidbody.AddForce(move * moveForce);

        //Rotation, current roation
        Vector3 rotationVector = transform.rotation.eulerAngles;

        //Calculate pitch and yaw rotations
        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];

        // Calculate smooth rotation changes
        smootPithChange = Mathf.MoveTowards(smootPithChange, pitchChange, 2f * Time.fixedDeltaTime);
        smootYawChange = Mathf.MoveTowards(smootYawChange, yawChange, 2f * Time.fixedDeltaTime);

        //Calculate pitch and yaw basesd on smoother values
        //Clamp pitch to avoid flipping
        float pitch = rotationVector.x + smootPithChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f; //For values negatives and positives (270 = -90)
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smootYawChange * Time.fixedDeltaTime * yawSpeed;

        //Apply new rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Collect vector observation from the enviromient, the amount of observation (10) are the Vector observation -> Space Size in the Unity
    /// editor in the Behavior parameters!
    /// </summary>
    /// <param name="sensor">Vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {

        // If nearest flower not set yet, observe empty array and return early
        if(nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        //Observe agent local rotation, realtive to the island (map) 4 observations
        sensor.AddObservation(transform.localRotation.normalized);

        //Get a vector from the beak tip to nearest flower
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;

        //Observe normalize vecot pointing to neares flower, normalized is a just a unit long () we need just the direction, 3 observation
        sensor.AddObservation(toFlower.normalized);

        // observe dot product that indicates the beak tip IS IN FRONT of flower () not behind, 1 observation
        // (+1 means beak is in front, -1 meeans behind)
        // Mirarse doc del producto de vectores como sabe que está detrás o delante
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe a dot product that indicates that the beak is POINTING toward the flower, 1 observation
        // +1 = beak is pointing directly at the flower, -1 means directly away
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));


        // Observe distance from beak to tle flower, 1 observation (between 0 and 1 CREO)
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);

        // 10 total observations!

    }

    /// <summary>
    /// When Behavior Type is set to "Heuristic Only" on the agent's Behavior Parameters,
    /// this function will be called, Its return values will be fed into
    /// <see cref="OnActionReceived(float[])"/> instead of using the NN.
    /// An algorithm makes the decision for the action instead of the NN (when player paying, player testing...)
    /// </summary>
    /// <param name="actionsOut">An ouput action array</param>
    public override void Heuristic(float[] actionsOut)
    {
        //Create placeholders for movments/turn
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        //Convert keyboard input to movement and turning
        //All values between -1 and 1

        // Forward/backward
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        //Left/right
        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        //Up/down
        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        //pitch up/down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = -1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = 1f;

        //yaw left/right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        //Combine movment vectors and normalize
        Vector3 combined = (forward + left + up).normalized;

        //Add the 3 movement values, pitch and yaw to the actionsOut array
        actionsOut[0] = combined.x;
        actionsOut[1] = combined.y;
        actionsOut[2] = combined.z;
        actionsOut[3] = pitch;
        actionsOut[4] = yaw;

    }

    /// <summary>
    /// Prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze bot supported in traingng");
        frozen = true;
        rigidbody.Sleep();

    }

    /// <summary>
    /// Resume taking actions
    /// </summary>
    public void UnFreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze bot supported in traingng");
        frozen = false;
        rigidbody.WakeUp();

    }

    /// <summary>
    /// Move agent to safe rand pos (not collide with anythigs
    /// if in front of flower -> point beak to it
    /// </summary>
    /// <param name="inFrontOfFlower"></param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;

        int attempsRemaining = 100;

        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentitialRotation = new Quaternion();

        while(!safePositionFound && attempsRemaining > 0)
        {
            attempsRemaining--;
            if (inFrontOfFlower)
            {
                //pick arandom flower
                Flower randomflower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // position 20cm in front of wloers
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomflower.transform.position + randomflower.FlowerUpVector * distanceFromFlower;

                //Point beak at flower
                Vector3 toFlower = randomflower.FlowerCenterPosition - potentialPosition;
                potentitialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                //Random radius form center of the area
                float radius = UnityEngine.Random.Range(2f, 7f);

                //Random direction
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180), 0);

                //Combine height, radius and direction
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                //Choose random pitch and yaw
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180);

                potentitialRotation = Quaternion.Euler(pitch, yaw, 0);
            }

            //Check to see if agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            //Safe position found if no colliders found
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "No safe position can be found :(");

        //Set pos and rot
        transform.position = potentialPosition;
        transform.rotation = potentitialRotation;

    }

    /// <summary>
    /// Upd nearest flower to the agent (give target and keep that until it succesfully reaches it) to avoid swaping nearest flower fast
    /// </summary>
    private void UpdateNearestFlower()
    {
        foreach(Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.hasNectar)
            {
                //No current nearest flower and this flower has nectar, set to this flower
                nearestFlower = flower;
            }
            else if(flower.hasNectar)
            {
                // Calculate distance to this flower and distance to the current nearest flower
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                //If nearest flower is empty or this flower is closer -> upd nearest flower
                if(distanceToCurrentNearestFlower > distanceToFlower || !nearestFlower.hasNectar)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    /// <summary>
    /// Called when the agents collider enters a trigger collider
    /// </summary>
    /// <param name="other">The trigger collider</param>
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);
    }


    /// <summary>
    /// Called (every update) when the agents collider stays in a trigger collider
    /// </summary>
    /// <param name="other">The trigger collider</param>
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Handles when agents collider enter or stays in trigger collider
    /// </summary>
    /// <param name="collider">Trigger collider</param>
    private void TriggerEnterOrStay(Collider collider)
    {
        //Agnet if colliding with nectar???
        //Check if other thing (head, wings) is colliding with the nectar
        //closestpoint to beak should be the same (or less hat the radious previusly defined) to beaktip
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            //Check if closest collision point is close to the beaktip
            //Note collision with anything but th beaktip should not count
            if(Vector3.Distance(beakTip.position, closestPointToBeakTip) < BeakTipRadius)
            {
                //Look up the flower for this nectar colldier
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                //attempo to take 0.01 nectar
                // Note: this is per fixed timestep, meaning it happens every .02 seconds or 50x per sencond
                float nectarReceived = flower.Feed(.01f);

                //Keep track of nectar obtained
                NectarObtained += nectarReceived;

                if (trainingMode)
                {
                    //Calculate reward for getting nectar and a bonus for pointing directly at the flower
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));
                    AddReward(.01f + bonus);
                }

                //If flower is empty, update nearest flower
                if (!flower.hasNectar)
                {
                    UpdateNearestFlower();
                }

            }
        }
    }


    /// <summary>
    /// Called when agent enters something solid (not trigger)
    /// Solo cuando entra, si fuera en stay daria mazo negative rewards y solo sabría lo que está mal (lo bueno es muy poco bueno) :(
    /// </summary>
    /// <param name="collision">Collision info</param>
    private void OnCollisionEnter(Collision collision)
    {
        if(trainingMode && collision.collider.CompareTag("boundary"))
        {
            //Collided with area boundary -> give negative reward
            AddReward(-0.5f);
        }
    }

    /// <summary>
    /// Called every frame
    /// </summary>
    private void Update()
    {
        // draw lie from beaktip to neares flower
        if(nearestFlower != null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
        }
    }


    /// <summary>
    /// Called evry 0.02s
    /// </summary>
    private void FixedUpdate()
    {
        //Prevent scenario where nearest flower nectar is stolen by an opponent and the neearestflower is not updated
        if(nearestFlower != null && !nearestFlower.hasNectar)
        {
            UpdateNearestFlower();
        }
    }
}

// When the NN imported, there is a (-1,1,1,46) vector observartion
// of those last 46, 10 are the ones we defined in the code (movements and rots)
// and 36 should be the 3 RayPerceptionSensor3D we added to the bird!

//Reducing the number of hidden units will result in a lighter NN and ligher computational power
