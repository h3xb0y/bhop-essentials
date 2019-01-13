using UnityEngine;

public class StrafeMovement : MonoBehaviour
{
  private const float NOCLIP_SPEED = 10f;

  public float accel = 200f;
  public float airAccel = 200f;
  public float maxSpeed = 6.4f;
  public float maxAirSpeed = 0.6f;
  public float friction = 8f;
  public float jumpForce = 5f;
  public float maxStepHeight = 0.201f;
  public LayerMask groundLayers;

  private GameObject camObj;
  public float lastJumpPress = -1f;
  public float jumpPressDuration = 0.1f;
  private bool onGround;

  private bool frozen;

  private bool jumpKeyPressed;

  private Vector3 lastFrameVelocity = Vector3.zero;

  private void Awake()
  {
    camObj = transform.Find("Camera").gameObject;
  }

  private void Update()
  {
    if (frozen)
      return;

    jumpKeyPressed = Input.GetButton("Jump");

    if (jumpKeyPressed)
    {
      lastJumpPress = Time.time;
    }
  }

  public void Freeze()
  {
    frozen = true;
    GetComponent<Rigidbody>().isKinematic = true;
  }

  public void Unfreeze()
  {
    GetComponent<Rigidbody>().isKinematic = false;
    frozen = false;
  }

  public void ResetVelocity()
  {
    GetComponent<Rigidbody>().velocity = Vector3.zero;
  }

  private void FixedUpdate()
  {
    Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

    Vector3 tempVelocity = CalculateFriction(GetComponent<Rigidbody>().velocity);

    tempVelocity += CalculateMovement(input, tempVelocity);

    if (!GetComponent<Rigidbody>().isKinematic)
    {
      GetComponent<Rigidbody>().velocity = tempVelocity;
    }

    lastFrameVelocity = GetComponent<Rigidbody>().velocity;
  }

  public bool Noclip
  {
    set
    {
      GetComponent<Rigidbody>().useGravity = !value;
      GetComponent<Collider>().enabled = !value;
    }
    get { return !GetComponent<Rigidbody>().useGravity; }
  }

  private Vector3 CalculateFriction(Vector3 currentVelocity)
  {
    if (Noclip)
      return Vector3.zero;

    onGround = CheckGround();
    float speed = currentVelocity.magnitude;

    //Code from https://flafla2.github.io/2015/02/14/bunnyhop.html
    if (!onGround || Input.GetButton("Jump") || speed == 0f)
      return currentVelocity;

    float drop = speed * friction * Time.deltaTime;
    return currentVelocity * (Mathf.Max(speed - drop, 0f) / speed);
  }

  public Vector3 CalculateMovement(Vector2 input, Vector3 velocity)
  {
    if (Noclip)
      return camObj.transform.rotation * new Vector3(input.x * NOCLIP_SPEED, 0f, input.y * NOCLIP_SPEED);

    onGround = CheckGround();

    float curAccel = accel;
    if (!onGround)
      curAccel = airAccel;

    float curMaxSpeed = maxSpeed;

    //Air speed
    if (!onGround)
      curMaxSpeed = maxAirSpeed;

    Vector3 camRotation = new Vector3(0f, camObj.transform.rotation.eulerAngles.y, 0f);
    Vector3 inputVelocity = Quaternion.Euler(camRotation) *
                            new Vector3(input.x * curAccel, 0f, input.y * curAccel);

    Vector3 alignedInputVelocity = new Vector3(inputVelocity.x, 0f, inputVelocity.z) * Time.deltaTime;

    Vector3 currentVelocity = new Vector3(velocity.x, 0f, velocity.z);

    float max = Mathf.Max(0f, 1 - (currentVelocity.magnitude / curMaxSpeed));

    float velocityDot = Vector3.Dot(currentVelocity, alignedInputVelocity);

    Vector3 modifiedVelocity = alignedInputVelocity * max;

    Vector3 correctVelocity = Vector3.Lerp(alignedInputVelocity, modifiedVelocity, velocityDot);

    correctVelocity += GetJumpVelocity(velocity.y);

    return correctVelocity;
  }

  private Vector3 GetJumpVelocity(float yVelocity)
  {
    Vector3 jumpVelocity = Vector3.zero;

    if (Time.time < lastJumpPress + jumpPressDuration && yVelocity < jumpForce && CheckGround())
    {
      lastJumpPress = -1f;
      // TODO @SOUND @NICE play jumping sound
      jumpVelocity = new Vector3(0f, jumpForce - yVelocity, 0f);
    }

    return jumpVelocity;
  }

  private void OnCollisionEnter(Collision col)
  {
    bool doStepUp = true;
    float footHeight = transform.position.y - GetComponent<Collider>().bounds.extents.y;

    foreach (ContactPoint p in col.contacts)
    {
      Debug.DrawLine(p.point, p.point + p.normal, Color.red, 1f);

      if (p.otherCollider is BoxCollider)
      {
        if (footHeight + maxStepHeight < p.otherCollider.transform.position.y +
            p.otherCollider.bounds.extents.y)
          doStepUp = false;
      }
      else if (p.otherCollider is MeshCollider)
      {
        // TODO do stuff here
        doStepUp = false;
      }
    }

    if (doStepUp)
    {
      // TODO check if there is space for the player
      transform.position = new Vector3(transform.position.x,
        col.collider.transform.position.y + col.collider.bounds.extents.y +
        GetComponent<Collider>().bounds.extents.y + 0.001f, transform.position.z);
      GetComponent<Rigidbody>().velocity = lastFrameVelocity;
    }
  }

  public bool CheckGround()
  {
    Vector3 pos = new Vector3(transform.position.x,
      transform.position.y - GetComponent<Collider>().bounds.extents.y + 0.05f, transform.position.z);
    Vector3 radiusVector = new Vector3(GetComponent<Collider>().bounds.extents.x, 0f, 0f);
    return CheckCylinder(pos, radiusVector, -0.1f, 8);
  }

  private bool CheckCylinder(Vector3 origin, Vector3 radiusVector, float verticalLength, int rayCount,
    out float dist, bool slopeCheck = true)
  {
    bool tempHit = false;
    float tempDist = -1f;

    for (int i = -1; i < rayCount; i++)
    {
      RaycastHit hit;
      bool hasHit;
      float verticalDirection = Mathf.Sign(verticalLength);

      if (i == -1) 
      {
        hasHit = Physics.Raycast(origin, Vector3.up * verticalDirection, out hit, Mathf.Abs(verticalLength),
          groundLayers);
      }
      else 
      {
        Vector3 radius = Quaternion.Euler(new Vector3(0f, i * (360f / rayCount), 0f)) * radiusVector;
        Vector3 circlePoint = origin + radius;

        hasHit = Physics.Raycast(circlePoint, Vector3.up * verticalDirection, out hit,
          Mathf.Abs(verticalLength), groundLayers);
      }

      if (!hasHit) continue;
      
      if (tempDist == -1f)
        tempDist = hit.distance;
      else if (tempDist > hit.distance)
        tempDist = hit.distance;

      if (!slopeCheck || hit.normal.y > 0.75f)
      {
        tempHit = true;
      }
    }

    dist = tempDist;

    return tempHit;
  }

  private bool CheckCylinder(Vector3 origin, Vector3 radiusVector, float verticalLength, int rayCount,
    bool slopeCheck = true)
  {
    float dist;
    return CheckCylinder(origin, radiusVector, verticalLength, rayCount, out dist, slopeCheck);
  }
}